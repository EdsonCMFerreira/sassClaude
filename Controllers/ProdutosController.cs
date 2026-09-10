using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;
using Saas.Models;

namespace Saas.Controllers;

[Authorize]
public class ProdutosController : Controller
{
    private readonly SassDbContext _context;

    public ProdutosController(SassDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    public IActionResult Vencidos() => View();

    public IActionResult VencendoEm30Dias() => View();

    public IActionResult SaldoZero() => View();

    public async Task<IActionResult> Dashboard()
    {
        var produtos = await _context.Produtos.Include(x => x.Fornecedor).ToListAsync();
        var today = DateTime.UtcNow.Date;
        var in30Days = today.AddDays(30);

        var comPrecoDefinido = produtos.Where(p => p.ValorCompra > 0 && p.ValorVenda > 0).ToList();
        var margens = comPrecoDefinido
            .Select(p => (Produto: p, Margem: (p.ValorVenda - p.ValorCompra) / p.ValorCompra * 100))
            .ToList();

        var maisLucrativo = margens.OrderByDescending(x => x.Margem).FirstOrDefault();
        var menosLucrativo = margens.OrderBy(x => x.Margem).FirstOrDefault();

        var model = new ProdutoDashboardViewModel
        {
            TotalProdutos = produtos.Count,
            Vencidos = produtos.Count(p => p.Validade.Date < today),
            VenceEm30Dias = produtos.Count(p => p.Validade.Date >= today && p.Validade.Date <= in30Days),
            LucroMedioPercentual = margens.Count > 0 ? Math.Round(margens.Average(x => x.Margem), 2) : 0m,
            ValorTotalCompra = produtos.Sum(p => p.ValorCompra),
            ValorTotalVenda = produtos.Sum(p => p.ValorVenda),
            LucroPotencialTotal = produtos.Sum(p => p.ValorVenda - p.ValorCompra),
            ProximosVencimentos = produtos
                .Where(p => p.Validade.Date >= today)
                .OrderBy(p => p.Validade)
                .Take(5)
                .Select(p => new ProdutoExpiryRow(p.Codigo, p.Descricao, p.Validade, (p.Validade.Date - today).Days))
                .ToList(),
            TopFornecedores = produtos
                .GroupBy(p => p.Fornecedor?.Nome ?? "—")
                .Select(g => new SupplierBreakdownRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Quantidade)
                .Take(5)
                .ToList(),
            ProdutoMaisLucrativo = maisLucrativo.Produto is not null ? $"{maisLucrativo.Produto.Codigo} — {maisLucrativo.Produto.Descricao}" : null,
            ProdutoMaisLucrativoPercentual = maisLucrativo.Produto is not null ? Math.Round(maisLucrativo.Margem, 2) : null,
            ProdutoMenosLucrativo = menosLucrativo.Produto is not null ? $"{menosLucrativo.Produto.Codigo} — {menosLucrativo.Produto.Descricao}" : null,
            ProdutoMenosLucrativoPercentual = menosLucrativo.Produto is not null ? Math.Round(menosLucrativo.Margem, 2) : null,
            ProdutosPorLucro = margens
                .OrderByDescending(x => x.Margem)
                .Take(8)
                .Select(x => new ProdutoLucroRow(x.Produto.Descricao, Math.Round(x.Margem, 2)))
                .ToList()
        };

        return View(model);
    }
}

[ApiController]
[Authorize]
[Route("api/produtos")]
public class ApiProdutosController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiProdutosController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProdutoResponse>>> GetProdutos()
    {
        var produtos = await _context.Produtos.Include(x => x.Fornecedor).OrderBy(x => x.Codigo).ToListAsync();
        var pedidoQuantidades = await _context.PedidoItens
            .Where(i => i.ProdutoId != null)
            .GroupBy(i => i.ProdutoId!.Value)
            .Select(g => new { ProdutoId = g.Key, Total = g.Sum(i => i.Quantidade) })
            .ToDictionaryAsync(x => x.ProdutoId, x => x.Total);
        return produtos.Select(p => ToResponse(p, pedidoQuantidades.GetValueOrDefault(p.Id))).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProdutoResponse>> GetProduto(int id)
    {
        var produto = await _context.Produtos.Include(x => x.Fornecedor).FirstOrDefaultAsync(x => x.Id == id);
        if (produto is null)
        {
            return NotFound();
        }

        var pedidoQuantidade = await _context.PedidoItens.Where(i => i.ProdutoId == id).SumAsync(i => (int?)i.Quantidade) ?? 0;
        return ToResponse(produto, pedidoQuantidade);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProdutoResponse>> PostProduto(ProdutoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest("Código e descrição são obrigatórios.");
        }

        if (request.ValorVenda <= request.ValorCompra)
        {
            return BadRequest("O valor de venda deve ser maior que o valor de compra.");
        }

        if (request.Quantidade < 0)
        {
            return BadRequest("A quantidade não pode ser negativa.");
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var codigo = request.Codigo.Trim();
        var alreadyExists = await _context.Produtos.AnyAsync(item => item.Codigo == codigo);
        if (alreadyExists)
        {
            return BadRequest("Já existe um produto com esse código.");
        }

        var produto = new Produto
        {
            Codigo = codigo,
            Descricao = request.Descricao.Trim(),
            Validade = request.Validade,
            ValorCompra = request.ValorCompra,
            ValorVenda = request.ValorVenda,
            Quantidade = request.Quantidade,
            Fornecedor = fornecedor,
            CreatedAt = DateTime.UtcNow
        };

        _context.Produtos.Add(produto);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProduto), new { id = produto.Id }, ToResponse(produto, 0));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutProduto(int id, ProdutoRequest request)
    {
        var existing = await _context.Produtos.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest("Código e descrição são obrigatórios.");
        }

        if (request.ValorVenda <= request.ValorCompra)
        {
            return BadRequest("O valor de venda deve ser maior que o valor de compra.");
        }

        if (request.Quantidade < 0)
        {
            return BadRequest("A quantidade não pode ser negativa.");
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var codigo = request.Codigo.Trim();
        var codeTaken = await _context.Produtos.AnyAsync(item => item.Id != id && item.Codigo == codigo);
        if (codeTaken)
        {
            return BadRequest("Já existe um produto com esse código.");
        }

        existing.Codigo = codigo;
        existing.Descricao = request.Descricao.Trim();
        existing.Validade = request.Validade;
        existing.ValorCompra = request.ValorCompra;
        existing.ValorVenda = request.ValorVenda;
        existing.Quantidade = request.Quantidade;
        existing.Fornecedor = fornecedor;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduto(int id)
    {
        var produto = await _context.Produtos.FindAsync(id);
        if (produto is null)
        {
            return NotFound();
        }

        _context.Produtos.Remove(produto);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static ProdutoResponse ToResponse(Produto produto, int quantidadePedida)
    {
        var percentualLucro = produto.ValorCompra > 0
            ? Math.Round((produto.ValorVenda - produto.ValorCompra) / produto.ValorCompra * 100, 2)
            : 0m;

        return new ProdutoResponse(
            produto.Id,
            produto.Codigo,
            produto.Descricao,
            produto.Validade,
            produto.ValorCompra,
            produto.ValorVenda,
            percentualLucro,
            produto.FornecedorId ?? 0,
            produto.Fornecedor?.Nome ?? "—",
            produto.Quantidade,
            produto.Quantidade - quantidadePedida,
            produto.CreatedAt);
    }
}

public sealed record ProdutoRequest(string Codigo, string Descricao, DateTime Validade, decimal ValorCompra, decimal ValorVenda, int FornecedorId, int Quantidade);

public sealed record ProdutoResponse(int Id, string Codigo, string Descricao, DateTime Validade, decimal ValorCompra, decimal ValorVenda, decimal PercentualLucro, int FornecedorId, string FornecedorNome, int Quantidade, int Saldo, DateTime CreatedAt);
