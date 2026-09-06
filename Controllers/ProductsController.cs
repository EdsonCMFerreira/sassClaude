using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class ProductsController : Controller
{
    private readonly SassDbContext _context;

    public ProductsController(SassDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Estoque()
    {
        var products = await _context.Products.Include(x => x.Fornecedor).ToListAsync();
        var compras = await _context.Compras.Where(c => c.ProductId != null).ToListAsync();
        var vendas = await _context.Vendas.Where(v => v.ProductId != null).ToListAsync();

        var itens = products
            .Select(p =>
            {
                var comprada = compras.Where(c => c.ProductId == p.Id).Sum(c => c.Quantidade);
                var vendida = vendas.Where(v => v.ProductId == p.Id).Sum(v => v.Quantidade);
                var saldo = comprada - vendida;
                return new EstoqueRow(p.Codigo, p.Descricao, p.Fornecedor?.Nome ?? "—", comprada, vendida, saldo, saldo * p.ValorCompra);
            })
            .OrderBy(x => x.Codigo)
            .ToList();

        var model = new EstoqueViewModel
        {
            Itens = itens,
            ValorTotalEstoque = itens.Sum(x => x.ValorEstoque),
            ProdutosComSaldoNegativo = itens.Count(x => x.SaldoEstoque < 0),
            ProdutosSemMovimentacao = itens.Count(x => x.QuantidadeComprada == 0 && x.QuantidadeVendida == 0)
        };

        return View(model);
    }

    public async Task<IActionResult> Dashboard()
    {
        var products = await _context.Products.Include(x => x.Fornecedor).ToListAsync();
        var today = DateTime.UtcNow.Date;
        var in30Days = today.AddDays(30);

        var comPrecoDefinido = products.Where(p => p.ValorCompra > 0 && p.ValorVenda > 0).ToList();
        var margens = comPrecoDefinido
            .Select(p => (Produto: p, Margem: (p.ValorVenda - p.ValorCompra) / p.ValorCompra * 100))
            .ToList();

        var maisLucrativo = margens.OrderByDescending(x => x.Margem).FirstOrDefault();
        var menosLucrativo = margens.OrderBy(x => x.Margem).FirstOrDefault();

        var model = new ProductDashboardViewModel
        {
            TotalProdutos = products.Count,
            Vencidos = products.Count(p => p.Validade.Date < today),
            VenceEm30Dias = products.Count(p => p.Validade.Date >= today && p.Validade.Date <= in30Days),
            LucroMedioPercentual = margens.Count > 0 ? Math.Round(margens.Average(x => x.Margem), 2) : 0m,
            ValorTotalCompra = products.Sum(p => p.ValorCompra),
            ValorTotalVenda = products.Sum(p => p.ValorVenda),
            LucroPotencialTotal = products.Sum(p => p.ValorVenda - p.ValorCompra),
            ProximosVencimentos = products
                .Where(p => p.Validade.Date >= today)
                .OrderBy(p => p.Validade)
                .Take(5)
                .Select(p => new ProductExpiryRow(p.Codigo, p.Descricao, p.Validade, (p.Validade.Date - today).Days))
                .ToList(),
            TopFornecedores = products
                .GroupBy(p => p.Fornecedor?.Nome ?? "—")
                .Select(g => new SupplierBreakdownRow(g.Key, g.Count()))
                .OrderByDescending(x => x.Quantidade)
                .Take(5)
                .ToList(),
            ProdutoMaisLucrativo = maisLucrativo.Produto is not null ? $"{maisLucrativo.Produto.Codigo} — {maisLucrativo.Produto.Descricao}" : null,
            ProdutoMaisLucrativoPercentual = maisLucrativo.Produto is not null ? Math.Round(maisLucrativo.Margem, 2) : null,
            ProdutoMenosLucrativo = menosLucrativo.Produto is not null ? $"{menosLucrativo.Produto.Codigo} — {menosLucrativo.Produto.Descricao}" : null,
            ProdutoMenosLucrativoPercentual = menosLucrativo.Produto is not null ? Math.Round(menosLucrativo.Margem, 2) : null
        };

        return View(model);
    }
}

[ApiController]
[Authorize]
[Route("api/products")]
public class ApiProductsController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiProductsController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProductResponse>>> GetProducts()
    {
        var products = await _context.Products.Include(x => x.Fornecedor).OrderBy(x => x.Codigo).ToListAsync();
        return products.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(int id)
    {
        var product = await _context.Products.Include(x => x.Fornecedor).FirstOrDefaultAsync(x => x.Id == id);
        if (product is null)
        {
            return NotFound();
        }

        return ToResponse(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<ProductResponse>> PostProduct(ProductRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest("Código e descrição são obrigatórios.");
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var codigo = request.Codigo.Trim();
        var alreadyExists = await _context.Products.AnyAsync(item => item.Codigo == codigo);
        if (alreadyExists)
        {
            return BadRequest("Já existe um produto com esse código.");
        }

        var product = new Product
        {
            Codigo = codigo,
            Descricao = request.Descricao.Trim(),
            Validade = request.Validade,
            ValorCompra = request.ValorCompra,
            ValorVenda = request.ValorVenda,
            Fornecedor = fornecedor,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, ToResponse(product));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutProduct(int id, ProductRequest request)
    {
        var existing = await _context.Products.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Descricao))
        {
            return BadRequest("Código e descrição são obrigatórios.");
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var codigo = request.Codigo.Trim();
        var codeTaken = await _context.Products.AnyAsync(item => item.Id != id && item.Codigo == codigo);
        if (codeTaken)
        {
            return BadRequest("Já existe um produto com esse código.");
        }

        existing.Codigo = codigo;
        existing.Descricao = request.Descricao.Trim();
        existing.Validade = request.Validade;
        existing.ValorCompra = request.ValorCompra;
        existing.ValorVenda = request.ValorVenda;
        existing.Fornecedor = fornecedor;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound();
        }

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static ProductResponse ToResponse(Product product)
    {
        var percentualLucro = product.ValorCompra > 0
            ? Math.Round((product.ValorVenda - product.ValorCompra) / product.ValorCompra * 100, 2)
            : 0m;

        return new ProductResponse(
            product.Id,
            product.Codigo,
            product.Descricao,
            product.Validade,
            product.ValorCompra,
            product.ValorVenda,
            percentualLucro,
            product.FornecedorId ?? 0,
            product.Fornecedor?.Nome ?? "—",
            product.CreatedAt);
    }
}

public sealed record ProductRequest(string Codigo, string Descricao, DateTime Validade, decimal ValorCompra, decimal ValorVenda, int FornecedorId);

public sealed record ProductResponse(int Id, string Codigo, string Descricao, DateTime Validade, decimal ValorCompra, decimal ValorVenda, decimal PercentualLucro, int FornecedorId, string FornecedorNome, DateTime CreatedAt);
