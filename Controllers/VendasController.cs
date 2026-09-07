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
            .Include(x => x.Itens).ThenInclude(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (venda is null)
        {
            return NotFound();
        }

        var valorTotal = venda.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
        var valorComDesconto = Math.Round(valorTotal * (1 - venda.PercentualDesconto / 100), 2);
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

                        foreach (var item in venda.Itens)
                        {
                            table.Cell().Text(item.Product?.Descricao ?? "—");
                            table.Cell().Text(item.Quantidade.ToString());
                            table.Cell().Text(item.ValorUnitario.ToString("C", ptBr));
                            table.Cell().Text((item.Quantidade * item.ValorUnitario).ToString("C", ptBr));
                        }
                    });

                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    if (venda.PercentualDesconto > 0)
                    {
                        column.Item().PaddingTop(10).AlignRight().Text($"Subtotal: {valorTotal.ToString("C", ptBr)}");
                        column.Item().AlignRight().Text($"Desconto: {venda.PercentualDesconto.ToString("0.##", ptBr)}%");
                        column.Item().AlignRight().Text($"Valor total: {valorComDesconto.ToString("C", ptBr)}").FontSize(14).Bold();
                    }
                    else
                    {
                        column.Item().PaddingTop(10).AlignRight().Text($"Valor total: {valorTotal.ToString("C", ptBr)}").FontSize(14).Bold();
                    }

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
            .Include(x => x.Itens).ThenInclude(x => x.Product)
            .OrderByDescending(x => x.DataVenda)
            .ToListAsync();
        return vendas.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<VendaResponse>> GetVenda(int id)
    {
        var venda = await _context.Vendas
            .Include(x => x.Cliente)
            .Include(x => x.Itens).ThenInclude(x => x.Product)
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
        if (request.Itens is null || request.Itens.Count == 0)
        {
            return BadRequest("Adicione ao menos um produto à venda.");
        }

        if (request.Itens.Any(i => i.Quantidade <= 0))
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        if (request.PercentualDesconto < 0 || request.PercentualDesconto > 100)
        {
            return BadRequest("O desconto deve estar entre 0 e 100%.");
        }

        var cliente = await _context.Clientes.FindAsync(request.ClienteId);
        if (cliente is null)
        {
            return BadRequest("Selecione um cliente válido.");
        }

        var produtosPorId = new Dictionary<int, Product>();
        foreach (var grupo in request.Itens.GroupBy(i => i.ProductId))
        {
            var product = await _context.Products.FindAsync(grupo.Key);
            if (product is null)
            {
                return BadRequest("Selecione um produto válido.");
            }

            var quantidadeTotal = grupo.Sum(i => i.Quantidade);
            if (quantidadeTotal > product.Saldo)
            {
                return BadRequest($"Estoque insuficiente. Saldo disponível de \"{product.Descricao}\": {product.Saldo}.");
            }

            produtosPorId[grupo.Key] = product;
        }

        var venda = new Venda
        {
            Cliente = cliente,
            DataVenda = request.DataVenda,
            NumeroNota = request.NumeroNota.Trim(),
            FormaPagamento = request.FormaPagamento.Trim(),
            Status = request.Status.Trim(),
            PercentualDesconto = request.PercentualDesconto,
            Observacoes = request.Observacoes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Itens)
        {
            venda.Itens.Add(new VendaItem
            {
                Product = produtosPorId[item.ProductId],
                Quantidade = item.Quantidade,
                ValorUnitario = item.ValorUnitario
            });
        }

        foreach (var grupo in request.Itens.GroupBy(i => i.ProductId))
        {
            produtosPorId[grupo.Key].Saldo -= grupo.Sum(i => i.Quantidade);
        }

        _context.Vendas.Add(venda);
        await _context.SaveChangesAsync();
        await RecalcularUltimaCompraCliente(cliente.Id);

        var vendaCompleta = await _context.Vendas
            .Include(x => x.Cliente)
            .Include(x => x.Itens).ThenInclude(x => x.Product)
            .FirstAsync(x => x.Id == venda.Id);

        return CreatedAtAction(nameof(GetVenda), new { id = venda.Id }, ToResponse(vendaCompleta));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutVenda(int id, VendaRequest request)
    {
        var existing = await _context.Vendas
            .Include(x => x.Itens)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        if (request.Itens is null || request.Itens.Count == 0)
        {
            return BadRequest("Adicione ao menos um produto à venda.");
        }

        if (request.Itens.Any(i => i.Quantidade <= 0))
        {
            return BadRequest("A quantidade deve ser maior que zero.");
        }

        if (request.PercentualDesconto < 0 || request.PercentualDesconto > 100)
        {
            return BadRequest("O desconto deve estar entre 0 e 100%.");
        }

        var cliente = await _context.Clientes.FindAsync(request.ClienteId);
        if (cliente is null)
        {
            return BadRequest("Selecione um cliente válido.");
        }

        var itensAntigos = existing.Itens.ToList();
        var produtosPorId = new Dictionary<int, Product>();

        foreach (var grupo in itensAntigos.Where(i => i.ProductId.HasValue).GroupBy(i => i.ProductId!.Value))
        {
            var product = await _context.Products.FindAsync(grupo.Key);
            if (product is not null)
            {
                product.Saldo += grupo.Sum(i => i.Quantidade);
                produtosPorId[grupo.Key] = product;
            }
        }

        foreach (var grupo in request.Itens.GroupBy(i => i.ProductId))
        {
            if (!produtosPorId.TryGetValue(grupo.Key, out var product))
            {
                product = await _context.Products.FindAsync(grupo.Key);
                if (product is null)
                {
                    return BadRequest("Selecione um produto válido.");
                }

                produtosPorId[grupo.Key] = product;
            }

            var quantidadeTotal = grupo.Sum(i => i.Quantidade);
            if (quantidadeTotal > product.Saldo)
            {
                return BadRequest($"Estoque insuficiente. Saldo disponível de \"{product.Descricao}\": {product.Saldo}.");
            }
        }

        foreach (var grupo in request.Itens.GroupBy(i => i.ProductId))
        {
            produtosPorId[grupo.Key].Saldo -= grupo.Sum(i => i.Quantidade);
        }

        var clienteAnteriorId = existing.ClienteId;

        _context.VendaItens.RemoveRange(itensAntigos);
        existing.Itens = request.Itens.Select(item => new VendaItem
        {
            Product = produtosPorId[item.ProductId],
            Quantidade = item.Quantidade,
            ValorUnitario = item.ValorUnitario
        }).ToList();

        existing.Cliente = cliente;
        existing.DataVenda = request.DataVenda;
        existing.NumeroNota = request.NumeroNota.Trim();
        existing.FormaPagamento = request.FormaPagamento.Trim();
        existing.Status = request.Status.Trim();
        existing.PercentualDesconto = request.PercentualDesconto;
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
        var venda = await _context.Vendas
            .Include(x => x.Itens)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (venda is null)
        {
            return NotFound();
        }

        var clienteId = venda.ClienteId;

        foreach (var grupo in venda.Itens.Where(i => i.ProductId.HasValue).GroupBy(i => i.ProductId!.Value))
        {
            var produto = await _context.Products.FindAsync(grupo.Key);
            if (produto is not null)
            {
                produto.Saldo += grupo.Sum(i => i.Quantidade);
            }
        }

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
            .Include(v => v.Itens)
            .Where(v => v.ClienteId == clienteId)
            .OrderByDescending(v => v.DataVenda)
            .FirstOrDefaultAsync();

        cliente.UltimaCompraData = ultima?.DataVenda;
        cliente.UltimaCompraValor = ultima is null ? null : CalcularValorComDesconto(ValorTotalVenda(ultima), ultima.PercentualDesconto);
        await _context.SaveChangesAsync();
    }

    private static decimal ValorTotalVenda(Venda venda)
    {
        return venda.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
    }

    private static decimal CalcularValorComDesconto(decimal valorTotal, decimal percentualDesconto)
    {
        return Math.Round(valorTotal * (1 - percentualDesconto / 100), 2);
    }

    private static VendaResponse ToResponse(Venda venda)
    {
        var itens = venda.Itens.Select(i => new VendaItemResponse(
            i.Id,
            i.ProductId ?? 0,
            i.Product?.Descricao ?? "—",
            i.Quantidade,
            i.ValorUnitario,
            i.Quantidade * i.ValorUnitario)).ToList();

        var valorTotal = itens.Sum(i => i.ValorTotal);

        return new VendaResponse(
            venda.Id,
            venda.ClienteId ?? 0,
            venda.Cliente?.Nome ?? "—",
            itens,
            valorTotal,
            venda.PercentualDesconto,
            CalcularValorComDesconto(valorTotal, venda.PercentualDesconto),
            venda.DataVenda,
            venda.NumeroNota,
            venda.FormaPagamento,
            venda.Status,
            venda.Observacoes,
            venda.CreatedAt);
    }
}

public sealed record VendaItemRequest(int ProductId, int Quantidade, decimal ValorUnitario);

public sealed record VendaRequest(
    int ClienteId, List<VendaItemRequest> Itens, DateTime DataVenda,
    string NumeroNota, string FormaPagamento, string Status, decimal PercentualDesconto, string Observacoes);

public sealed record VendaItemResponse(int Id, int ProductId, string ProductNome, int Quantidade, decimal ValorUnitario, decimal ValorTotal);

public sealed record VendaResponse(
    int Id, int ClienteId, string ClienteNome, List<VendaItemResponse> Itens,
    decimal ValorTotal, decimal PercentualDesconto, decimal ValorComDesconto, DateTime DataVenda,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes, DateTime CreatedAt);
