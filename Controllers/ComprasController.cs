using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class ComprasController : Controller
{
    public IActionResult Index() => View();
}

[ApiController]
[Authorize]
[Route("api/compras")]
public class ApiComprasController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiComprasController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CompraResponse>>> GetCompras()
    {
        var compras = await _context.Compras
            .Include(x => x.Fornecedor)
            .Include(x => x.Product)
            .OrderByDescending(x => x.DataCompra)
            .ToListAsync();
        return compras.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CompraResponse>> GetCompra(int id)
    {
        var compra = await _context.Compras
            .Include(x => x.Fornecedor)
            .Include(x => x.Product)
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
        if (request.Quantidade <= 0)
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var product = await _context.Products.FindAsync(request.ProductId);
        if (product is null)
        {
            return BadRequest("Selecione um produto válido.");
        }

        var compra = new Compra
        {
            Fornecedor = fornecedor,
            Product = product,
            Quantidade = request.Quantidade,
            ValorUnitario = request.ValorUnitario,
            DataCompra = request.DataCompra,
            NumeroNota = request.NumeroNota.Trim(),
            FormaPagamento = request.FormaPagamento.Trim(),
            Status = request.Status.Trim(),
            Observacoes = request.Observacoes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Compras.Add(compra);
        await _context.SaveChangesAsync();
        await RecalcularUltimaCompraFornecedor(fornecedor.Id);

        return CreatedAtAction(nameof(GetCompra), new { id = compra.Id }, ToResponse(compra));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutCompra(int id, CompraRequest request)
    {
        var existing = await _context.Compras.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (request.Quantidade <= 0)
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        var fornecedor = await _context.Fornecedores.FindAsync(request.FornecedorId);
        if (fornecedor is null)
        {
            return BadRequest("Selecione um fornecedor válido.");
        }

        var product = await _context.Products.FindAsync(request.ProductId);
        if (product is null)
        {
            return BadRequest("Selecione um produto válido.");
        }

        var fornecedorAnteriorId = existing.FornecedorId;

        existing.Fornecedor = fornecedor;
        existing.Product = product;
        existing.Quantidade = request.Quantidade;
        existing.ValorUnitario = request.ValorUnitario;
        existing.DataCompra = request.DataCompra;
        existing.NumeroNota = request.NumeroNota.Trim();
        existing.FormaPagamento = request.FormaPagamento.Trim();
        existing.Status = request.Status.Trim();
        existing.Observacoes = request.Observacoes.Trim();

        await _context.SaveChangesAsync();

        if (fornecedorAnteriorId.HasValue && fornecedorAnteriorId != fornecedor.Id)
        {
            await RecalcularUltimaCompraFornecedor(fornecedorAnteriorId.Value);
        }

        await RecalcularUltimaCompraFornecedor(fornecedor.Id);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCompra(int id)
    {
        var compra = await _context.Compras.FindAsync(id);
        if (compra is null)
        {
            return NotFound();
        }

        var fornecedorId = compra.FornecedorId;
        _context.Compras.Remove(compra);
        await _context.SaveChangesAsync();

        if (fornecedorId.HasValue)
        {
            await RecalcularUltimaCompraFornecedor(fornecedorId.Value);
        }

        return NoContent();
    }

    private async Task RecalcularUltimaCompraFornecedor(int fornecedorId)
    {
        var fornecedor = await _context.Fornecedores.FindAsync(fornecedorId);
        if (fornecedor is null)
        {
            return;
        }

        var ultima = await _context.Compras
            .Where(c => c.FornecedorId == fornecedorId)
            .OrderByDescending(c => c.DataCompra)
            .FirstOrDefaultAsync();

        fornecedor.UltimaCompraData = ultima?.DataCompra;
        fornecedor.UltimaCompraValor = ultima is null ? null : ultima.Quantidade * ultima.ValorUnitario;
        await _context.SaveChangesAsync();
    }

    private static CompraResponse ToResponse(Compra compra)
    {
        return new CompraResponse(
            compra.Id,
            compra.FornecedorId ?? 0,
            compra.Fornecedor?.Nome ?? "—",
            compra.ProductId ?? 0,
            compra.Product?.Descricao ?? "—",
            compra.Quantidade,
            compra.ValorUnitario,
            compra.Quantidade * compra.ValorUnitario,
            compra.DataCompra,
            compra.NumeroNota,
            compra.FormaPagamento,
            compra.Status,
            compra.Observacoes,
            compra.CreatedAt);
    }
}

public sealed record CompraRequest(
    int FornecedorId, int ProductId, int Quantidade, decimal ValorUnitario, DateTime DataCompra,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes);

public sealed record CompraResponse(
    int Id, int FornecedorId, string FornecedorNome, int ProductId, string ProductNome,
    int Quantidade, decimal ValorUnitario, decimal ValorTotal, DateTime DataCompra,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes, DateTime CreatedAt);
