using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;
using Saas.Models;

namespace Saas.Controllers;

[Authorize(Policy = "AcessoCompras")]
public class ComprasController : Controller
{
    private readonly SassDbContext _context;

    public ComprasController(SassDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Dashboard()
    {
        var todasCompras = await _context.Compras
            .Include(c => c.Fornecedor)
            .Include(c => c.Itens).ThenInclude(i => i.Produto)
            .ToListAsync();

        decimal ValorCompra(Compra compra) => compra.Itens.Sum(i => i.Quantidade * i.ValorUnitario);

        var naoCanceladas = todasCompras.Where(c => c.Status != "Cancelada").ToList();

        var hoje = DateTime.UtcNow.Date;
        var gastoMensal = Enumerable.Range(0, 6)
            .Select(i => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i))
            .OrderBy(d => d)
            .Select(mes =>
            {
                var doMes = naoCanceladas.Where(c => c.DataCompra.Year == mes.Year && c.DataCompra.Month == mes.Month);
                var ptBr = new System.Globalization.CultureInfo("pt-BR");
                var rotulo = mes.ToString("MMM/yyyy", ptBr);
                return new GastoMensalRow(rotulo, doMes.Sum(ValorCompra));
            })
            .ToList();

        var topFornecedores = naoCanceladas
            .Where(c => c.FornecedorId.HasValue)
            .GroupBy(c => c.FornecedorId!.Value)
            .Select(g => new FornecedorGastoRow(g.First().Fornecedor?.Nome ?? "—", g.Sum(ValorCompra), g.Count()))
            .OrderByDescending(r => r.Total)
            .Take(10)
            .ToList();

        var produtosMaisComprados = naoCanceladas
            .SelectMany(c => c.Itens)
            .Where(i => i.ProdutoId.HasValue)
            .GroupBy(i => i.ProdutoId!.Value)
            .Select(g => new ProdutoMaisCompradoRow(g.First().Produto?.Descricao ?? "—", g.Sum(i => i.Quantidade)))
            .OrderByDescending(r => r.QuantidadeTotal)
            .Take(10)
            .ToList();

        var porStatus = todasCompras
            .GroupBy(c => c.Status)
            .Select(g => new StatusBreakdownRow(g.Key, g.Count()))
            .ToList();

        var model = new CompraDashboardViewModel
        {
            TotalCompras = todasCompras.Count,
            GastoTotal = naoCanceladas.Sum(ValorCompra),
            TotalEmAberto = naoCanceladas.Where(c => !c.DataPagamento.HasValue).Sum(ValorCompra),
            TotalPago = naoCanceladas.Where(c => c.DataPagamento.HasValue).Sum(ValorCompra),
            PorStatus = porStatus,
            GastoMensal = gastoMensal,
            TopFornecedores = topFornecedores,
            ProdutosMaisComprados = produtosMaisComprados
        };

        return View(model);
    }
}

[ApiController]
[Authorize(Policy = "AcessoCompras")]
[Route("api/compras")]
public class ApiComprasController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiComprasController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompraResponse>>> GetCompras([FromQuery] string? status)
    {
        var query = _context.Compras
            .Include(x => x.Fornecedor)
            .Include(x => x.Itens).ThenInclude(x => x.Produto)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var compras = await query.OrderByDescending(x => x.DataCompra).ToListAsync();
        return compras.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompraResponse>> GetCompra(int id)
    {
        var compra = await _context.Compras
            .Include(x => x.Fornecedor)
            .Include(x => x.Itens).ThenInclude(x => x.Produto)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (compra is null)
        {
            return NotFound();
        }

        return ToResponse(compra);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<CompraResponse>> PostCompra(CompraRequest request)
    {
        if (request.Itens is null || request.Itens.Count == 0)
        {
            return BadRequest("Adicione ao menos um produto à conta a pagar.");
        }

        if (request.Itens.Any(i => i.Quantidade <= 0))
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        var erroPagamento = ValidarPagamento(request.Status, request.NumeroNota, request.FormaPagamento);
        if (erroPagamento is not null)
        {
            return BadRequest(erroPagamento);
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var produtosPorId = new Dictionary<int, Produto>();
        foreach (var grupo in request.Itens.GroupBy(i => i.ProdutoId))
        {
            var produto = await _context.Produtos.FindAsync(grupo.Key);
            if (produto is null)
            {
                return BadRequest("Selecione um produto válido.");
            }

            produtosPorId[grupo.Key] = produto;
        }

        var proximoNumero = await _context.Compras.MaxAsync(c => (int?)c.NumeroCompra) ?? 0;

        var compra = new Compra
        {
            NumeroCompra = proximoNumero + 1,
            Fornecedor = fornecedor,
            DataCompra = request.DataCompra,
            DataVencimento = request.DataVencimento,
            DataPagamento = request.DataPagamento,
            NumeroNota = request.NumeroNota.Trim(),
            FormaPagamento = request.FormaPagamento.Trim(),
            Status = request.Status.Trim(),
            Observacoes = request.Observacoes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Itens)
        {
            compra.Itens.Add(new CompraItem
            {
                Produto = produtosPorId[item.ProdutoId],
                Quantidade = item.Quantidade,
                ValorUnitario = item.ValorUnitario
            });
        }

        _context.Compras.Add(compra);
        await _context.SaveChangesAsync();

        var compraCompleta = await _context.Compras
            .Include(x => x.Fornecedor)
            .Include(x => x.Itens).ThenInclude(x => x.Produto)
            .FirstAsync(x => x.Id == compra.Id);

        return CreatedAtAction(nameof(GetCompra), new { id = compra.Id }, ToResponse(compraCompleta));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutCompra(int id, CompraRequest request)
    {
        var existing = await _context.Compras
            .Include(x => x.Itens)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        if (request.Itens is null || request.Itens.Count == 0)
        {
            return BadRequest("Adicione ao menos um produto à conta a pagar.");
        }

        if (request.Itens.Any(i => i.Quantidade <= 0))
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        var erroPagamento = ValidarPagamento(request.Status, request.NumeroNota, request.FormaPagamento);
        if (erroPagamento is not null)
        {
            return BadRequest(erroPagamento);
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var produtosPorId = new Dictionary<int, Produto>();
        foreach (var grupo in request.Itens.GroupBy(i => i.ProdutoId))
        {
            var produto = await _context.Produtos.FindAsync(grupo.Key);
            if (produto is null)
            {
                return BadRequest("Selecione um produto válido.");
            }

            produtosPorId[grupo.Key] = produto;
        }

        _context.CompraItens.RemoveRange(existing.Itens);
        existing.Itens = request.Itens.Select(item => new CompraItem
        {
            Produto = produtosPorId[item.ProdutoId],
            Quantidade = item.Quantidade,
            ValorUnitario = item.ValorUnitario
        }).ToList();

        existing.Fornecedor = fornecedor;
        existing.DataCompra = request.DataCompra;
        existing.DataVencimento = request.DataVencimento;
        existing.DataPagamento = request.DataPagamento;
        existing.NumeroNota = request.NumeroNota.Trim();
        existing.FormaPagamento = request.FormaPagamento.Trim();
        existing.Status = request.Status.Trim();
        existing.Observacoes = request.Observacoes.Trim();

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompra(int id)
    {
        var compra = await _context.Compras.FirstOrDefaultAsync(x => x.Id == id);
        if (compra is null)
        {
            return NotFound();
        }

        _context.Compras.Remove(compra);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private static string? ValidarPagamento(string status, string numeroNota, string formaPagamento)
    {
        if (status.Trim() != "Paga")
        {
            return null;
        }

        var numeroNotaTrimmed = numeroNota.Trim();
        if (string.IsNullOrWhiteSpace(numeroNotaTrimmed) || numeroNotaTrimmed == "0")
        {
            return "Informe o número da nota para marcar a conta a pagar como paga.";
        }

        if (string.IsNullOrWhiteSpace(formaPagamento))
        {
            return "Selecione a forma de pagamento para marcar a conta a pagar como paga.";
        }

        return null;
    }

    private static CompraResponse ToResponse(Compra compra)
    {
        var itens = compra.Itens.Select(i => new CompraItemResponse(
            i.Id,
            i.ProdutoId ?? 0,
            i.Produto?.Descricao ?? "—",
            i.Quantidade,
            i.ValorUnitario,
            i.Quantidade * i.ValorUnitario)).ToList();

        var valorTotal = itens.Sum(i => i.ValorTotal);

        return new CompraResponse(
            compra.Id,
            compra.NumeroCompra,
            compra.FornecedorId ?? 0,
            compra.Fornecedor?.Nome ?? "—",
            itens,
            valorTotal,
            compra.DataCompra,
            compra.DataVencimento,
            compra.DataPagamento,
            compra.NumeroNota,
            compra.FormaPagamento,
            compra.Status,
            compra.Observacoes,
            compra.CreatedAt);
    }
}

public sealed record CompraItemRequest(int ProdutoId, int Quantidade, decimal ValorUnitario);

public sealed record CompraRequest(
    int FornecedorId, List<CompraItemRequest> Itens, DateTime DataCompra, DateTime? DataVencimento, DateTime? DataPagamento,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes);

public sealed record CompraItemResponse(int Id, int ProdutoId, string ProdutoNome, int Quantidade, decimal ValorUnitario, decimal ValorTotal);

public sealed record CompraResponse(
    int Id, int NumeroCompra, int FornecedorId, string FornecedorNome, List<CompraItemResponse> Itens, decimal ValorTotal,
    DateTime DataCompra, DateTime? DataVencimento, DateTime? DataPagamento,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes, DateTime CreatedAt);
