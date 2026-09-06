using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class VendasController : Controller
{
    private readonly SassDbContext _context;

    public VendasController(SassDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Recibo(int id)
    {
        var venda = await _context.Vendas
            .Include(x => x.Cliente)
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (venda is null)
        {
            return NotFound();
        }

        var valorTotal = venda.Quantidade * venda.ValorUnitario;
        var ptBr = new System.Globalization.CultureInfo("pt-BR");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text("sassClaude").FontSize(20).Bold();
                    column.Item().Text("Recibo de venda").FontSize(14).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"Recibo Nº {venda.Id}");
                    column.Item().Text($"Data: {venda.DataVenda:dd/MM/yyyy}");
                    if (!string.IsNullOrWhiteSpace(venda.NumeroNota))
                    {
                        column.Item().Text($"Nota: {venda.NumeroNota}");
                    }

                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().PaddingTop(10).Text($"Cliente: {venda.Cliente?.Nome ?? "—"}");
                    if (venda.Cliente is not null && !string.IsNullOrWhiteSpace(venda.Cliente.CpfCnpj))
                    {
                        column.Item().Text($"CPF/CNPJ: {venda.Cliente.CpfCnpj}");
                    }

                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Produto").Bold();
                            header.Cell().Text("Qtd.").Bold();
                            header.Cell().Text("Valor unit.").Bold();
                            header.Cell().Text("Total").Bold();
                        });

                        table.Cell().Text(venda.Product?.Descricao ?? "—");
                        table.Cell().Text(venda.Quantidade.ToString());
                        table.Cell().Text(venda.ValorUnitario.ToString("C", ptBr));
                        table.Cell().Text(valorTotal.ToString("C", ptBr));
                    });

                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().PaddingTop(10).AlignRight().Text($"Valor total: {valorTotal.ToString("C", ptBr)}").FontSize(14).Bold();
                    column.Item().Text($"Forma de pagamento: {(string.IsNullOrWhiteSpace(venda.FormaPagamento) ? "—" : venda.FormaPagamento)}");
                    column.Item().Text($"Status: {venda.Status}");

                    if (!string.IsNullOrWhiteSpace(venda.Observacoes))
                    {
                        column.Item().PaddingTop(10).Text($"Observações: {venda.Observacoes}");
                    }
                });

                page.Footer().AlignCenter().Text("Documento gerado automaticamente pelo sassClaude.").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"recibo-venda-{venda.Id}.pdf");
    }
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

        if (request.Quantidade > product.Saldo)
        {
            return BadRequest($"Estoque insuficiente. Saldo disponível de \"{product.Descricao}\": {product.Saldo}.");
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
        product.Saldo -= request.Quantidade;
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
        var produtoAnteriorId = existing.ProductId;
        var quantidadeAnterior = existing.Quantidade;

        var saldoDisponivel = produtoAnteriorId == product.Id
            ? product.Saldo + quantidadeAnterior
            : product.Saldo;
        if (request.Quantidade > saldoDisponivel)
        {
            return BadRequest($"Estoque insuficiente. Saldo disponível de \"{product.Descricao}\": {saldoDisponivel}.");
        }

        existing.Cliente = cliente;
        existing.Product = product;
        existing.Quantidade = request.Quantidade;
        existing.ValorUnitario = request.ValorUnitario;
        existing.DataVenda = request.DataVenda;
        existing.NumeroNota = request.NumeroNota.Trim();
        existing.FormaPagamento = request.FormaPagamento.Trim();
        existing.Status = request.Status.Trim();
        existing.Observacoes = request.Observacoes.Trim();

        if (produtoAnteriorId.HasValue && produtoAnteriorId != product.Id)
        {
            var produtoAnterior = await _context.Products.FindAsync(produtoAnteriorId.Value);
            if (produtoAnterior is not null)
            {
                produtoAnterior.Saldo += quantidadeAnterior;
            }

            product.Saldo -= request.Quantidade;
        }
        else
        {
            product.Saldo -= request.Quantidade - quantidadeAnterior;
        }

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
        var produtoId = venda.ProductId;
        var quantidade = venda.Quantidade;
        _context.Vendas.Remove(venda);

        if (produtoId.HasValue)
        {
            var produto = await _context.Products.FindAsync(produtoId.Value);
            if (produto is not null)
            {
                produto.Saldo += quantidade;
            }
        }

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
