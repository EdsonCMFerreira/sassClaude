using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class VendasController : Controller
{
    public IActionResult Index() => View();
}

[ApiController]
[Authorize]
[Route("api/vendas")]
public class ApiVendasController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiVendasController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<VendaResponse>>> GetVendas()
    {
        var vendas = await _context.Vendas
            .Include(x => x.Cliente)
            .Include(x => x.Product)
            .OrderByDescending(x => x.DataVenda)
            .ToListAsync();
        return vendas.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VendaResponse>> GetVenda(int id)
    {
        var venda = await _context.Vendas
            .Include(x => x.Cliente)
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (venda is null)
        {
            return NotFound();
        }

        return ToResponse(venda);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<VendaResponse>> PostVenda(VendaRequest request)
    {
        if (request.Quantidade <= 0)
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        var cliente = await _context.Clientes.FindAsync(request.ClienteId);
        if (cliente is null)
        {
            return BadRequest("Selecione um cliente válido.");
        }

        var product = await _context.Products.FindAsync(request.ProductId);
        if (product is null)
        {
            return BadRequest("Selecione um produto válido.");
        }

        var venda = new Venda
        {
            Cliente = cliente,
            Product = product,
            Quantidade = request.Quantidade,
            ValorUnitario = request.ValorUnitario,
            DataVenda = request.DataVenda,
            NumeroNota = request.NumeroNota.Trim(),
            FormaPagamento = request.FormaPagamento.Trim(),
            Status = request.Status.Trim(),
            Observacoes = request.Observacoes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.Vendas.Add(venda);
        await _context.SaveChangesAsync();
        await RecalcularUltimaCompraCliente(cliente.Id);

        return CreatedAtAction(nameof(GetVenda), new { id = venda.Id }, ToResponse(venda));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutVenda(int id, VendaRequest request)
    {
        var existing = await _context.Vendas.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (request.Quantidade <= 0)
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        var cliente = await _context.Clientes.FindAsync(request.ClienteId);
        if (cliente is null)
        {
            return BadRequest("Selecione um cliente válido.");
        }

        var product = await _context.Products.FindAsync(request.ProductId);
        if (product is null)
        {
            return BadRequest("Selecione um produto válido.");
        }

        var clienteAnteriorId = existing.ClienteId;

        existing.Cliente = cliente;
        existing.Product = product;
        existing.Quantidade = request.Quantidade;
        existing.ValorUnitario = request.ValorUnitario;
        existing.DataVenda = request.DataVenda;
        existing.NumeroNota = request.NumeroNota.Trim();
        existing.FormaPagamento = request.FormaPagamento.Trim();
        existing.Status = request.Status.Trim();
        existing.Observacoes = request.Observacoes.Trim();

        await _context.SaveChangesAsync();

        if (clienteAnteriorId.HasValue && clienteAnteriorId != cliente.Id)
        {
            await RecalcularUltimaCompraCliente(clienteAnteriorId.Value);
        }

        await RecalcularUltimaCompraCliente(cliente.Id);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteVenda(int id)
    {
        var venda = await _context.Vendas.FindAsync(id);
        if (venda is null)
        {
            return NotFound();
        }

        var clienteId = venda.ClienteId;
        _context.Vendas.Remove(venda);
        await _context.SaveChangesAsync();

        if (clienteId.HasValue)
        {
            await RecalcularUltimaCompraCliente(clienteId.Value);
        }

        return NoContent();
    }

    private async Task RecalcularUltimaCompraCliente(int clienteId)
    {
        var cliente = await _context.Clientes.FindAsync(clienteId);
        if (cliente is null)
        {
            return;
        }

        var ultima = await _context.Vendas
            .Where(v => v.ClienteId == clienteId)
            .OrderByDescending(v => v.DataVenda)
            .FirstOrDefaultAsync();

        cliente.UltimaCompraData = ultima?.DataVenda;
        cliente.UltimaCompraValor = ultima is null ? null : ultima.Quantidade * ultima.ValorUnitario;
        await _context.SaveChangesAsync();
    }

    private static VendaResponse ToResponse(Venda venda)
    {
        return new VendaResponse(
            venda.Id,
            venda.ClienteId ?? 0,
            venda.Cliente?.Nome ?? "—",
            venda.ProductId ?? 0,
            venda.Product?.Descricao ?? "—",
            venda.Quantidade,
            venda.ValorUnitario,
            venda.Quantidade * venda.ValorUnitario,
            venda.DataVenda,
            venda.NumeroNota,
            venda.FormaPagamento,
            venda.Status,
            venda.Observacoes,
            venda.CreatedAt);
    }
}

public sealed record VendaRequest(
    int ClienteId, int ProductId, int Quantidade, decimal ValorUnitario, DateTime DataVenda,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes);

public sealed record VendaResponse(
    int Id, int ClienteId, string ClienteNome, int ProductId, string ProductNome,
    int Quantidade, decimal ValorUnitario, decimal ValorTotal, DateTime DataVenda,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes, DateTime CreatedAt);
