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
public class PedidosController : Controller
{
    private readonly SassDbContext _context;

    public PedidosController(SassDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    public IActionResult Pendentes() => View("PorStatus", new PedidoStatusPageViewModel(
        "Pendente", "Pedidos pendentes", "Pedidos que ainda aguardam conclusão, do mais antigo para o mais novo.",
        OrdenarAscendente: true, MostrarRecibo: false));

    public IActionResult Concluidos() => View("PorStatus", new PedidoStatusPageViewModel(
        "Concluída", "Pedidos concluídos", "Pedidos já concluídos, do mais recente para o mais antigo.",
        OrdenarAscendente: false, MostrarRecibo: true));

    public IActionResult Cancelados() => View("PorStatus", new PedidoStatusPageViewModel(
        "Cancelada", "Pedidos cancelados", "Pedidos cancelados, do mais recente para o mais antigo.",
        OrdenarAscendente: false, MostrarRecibo: false));

    public IActionResult Devolvidos() => View("PorStatus", new PedidoStatusPageViewModel(
        "Devolução", "Pedidos devolvidos", "Pedidos com devolução registrada, do mais recente para o mais antigo.",
        OrdenarAscendente: false, MostrarRecibo: false));

    public async Task<IActionResult> Dashboard()
    {
        var pedidos = await _context.Pedidos
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .ToListAsync();

        decimal ValorComDesconto(Pedido p)
        {
            var total = p.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
            return Math.Round(total * (1 - p.PercentualDesconto / 100), 2);
        }

        var concluidos = pedidos.Where(p => p.Status == "Concluída").ToList();

        var porStatus = pedidos
            .GroupBy(p => p.Status)
            .Select(g => new StatusBreakdownRow(g.Key, g.Count()))
            .OrderByDescending(x => x.Quantidade)
            .ToList();

        var hoje = DateTime.UtcNow.Date;
        var faturamentoMensal = Enumerable.Range(0, 6)
            .Select(i => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i))
            .OrderBy(d => d)
            .Select(mes => new FaturamentoMensalRow(
                mes,
                concluidos.Where(p => p.DataPedido.Year == mes.Year && p.DataPedido.Month == mes.Month).Sum(ValorComDesconto)))
            .ToList();

        var produtosMaisPedidos = pedidos
            .SelectMany(p => p.Itens)
            .Where(i => i.Produto is not null)
            .GroupBy(i => i.Produto!.Descricao)
            .Select(g => new ProdutoMaisPedidoRow(g.Key, g.Sum(i => i.Quantidade)))
            .OrderByDescending(x => x.QuantidadeTotal)
            .Take(5)
            .ToList();

        var valorTotalConcluidos = concluidos.Sum(ValorComDesconto);

        var model = new PedidoDashboardViewModel
        {
            TotalPedidos = pedidos.Count,
            PedidosPendentes = pedidos.Count(p => p.Status == "Pendente"),
            ValorTotalConcluidos = valorTotalConcluidos,
            TicketMedio = concluidos.Count > 0 ? Math.Round(valorTotalConcluidos / concluidos.Count, 2) : 0m,
            PorStatus = porStatus,
            FaturamentoMensal = faturamentoMensal,
            ProdutosMaisPedidos = produtosMaisPedidos
        };

        return View(model);
    }

    public async Task<IActionResult> Recibo(int id)
    {
        var pedido = await _context.Pedidos
            .Include(x => x.Cliente)
            .Include(x => x.Itens).ThenInclude(x => x.Produto)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (pedido is null)
        {
            return NotFound();
        }

        if (pedido.Status != "Concluída")
        {
            return BadRequest("O recibo só pode ser gerado para pedidos concluídos.");
        }

        var valorTotal = pedido.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
        var valorComDesconto = Math.Round(valorTotal * (1 - pedido.PercentualDesconto / 100), 2);
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
                    column.Item().Text("Recibo de pedido").FontSize(14).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingVertical(20).Column(column =>
                {
                    column.Spacing(8);
                    column.Item().Text($"Pedido Nº {pedido.NumeroPedido}");
                    column.Item().Text($"Data: {pedido.DataPedido:dd/MM/yyyy}");
                    if (!string.IsNullOrWhiteSpace(pedido.NumeroNota))
                    {
                        column.Item().Text($"Nota: {pedido.NumeroNota}");
                    }

                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    column.Item().PaddingTop(10).Text($"Cliente: {pedido.Cliente?.Nome ?? "—"}");
                    if (pedido.Cliente is not null && !string.IsNullOrWhiteSpace(pedido.Cliente.CpfCnpj))
                    {
                        column.Item().Text($"CPF/CNPJ: {pedido.Cliente.CpfCnpj}");
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

                        foreach (var item in pedido.Itens)
                        {
                            table.Cell().Text(item.Produto?.Descricao ?? "—");
                            table.Cell().Text(item.Quantidade.ToString());
                            table.Cell().Text(item.ValorUnitario.ToString("C", ptBr));
                            table.Cell().Text((item.Quantidade * item.ValorUnitario).ToString("C", ptBr));
                        }
                    });

                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);

                    if (pedido.PercentualDesconto > 0)
                    {
                        column.Item().PaddingTop(10).AlignRight().Text($"Subtotal: {valorTotal.ToString("C", ptBr)}");
                        column.Item().AlignRight().Text($"Desconto: {pedido.PercentualDesconto.ToString("0.##", ptBr)}%");
                        column.Item().AlignRight().Text($"Valor total: {valorComDesconto.ToString("C", ptBr)}").FontSize(14).Bold();
                    }
                    else
                    {
                        column.Item().PaddingTop(10).AlignRight().Text($"Valor total: {valorTotal.ToString("C", ptBr)}").FontSize(14).Bold();
                    }

                    column.Item().Text($"Forma de pagamento: {(string.IsNullOrWhiteSpace(pedido.FormaPagamento) ? "—" : pedido.FormaPagamento)}");
                    column.Item().Text($"Status: {pedido.Status}");

                    if (!string.IsNullOrWhiteSpace(pedido.Observacoes))
                    {
                        column.Item().PaddingTop(10).Text($"Observações: {pedido.Observacoes}");
                    }
                });

                page.Footer().AlignCenter().Text("Documento gerado automaticamente pelo sassClaude.").FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });

        var pdfBytes = document.GeneratePdf();
        return File(pdfBytes, "application/pdf", $"recibo-pedido-{pedido.NumeroPedido}.pdf");
    }
}

[ApiController]
[Authorize]
[Route("api/pedidos")]
public class ApiPedidosController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiPedidosController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PedidoResponse>>> GetPedidos([FromQuery] string? status)
    {
        var query = _context.Pedidos
            .Include(x => x.Cliente)
            .Include(x => x.Itens).ThenInclude(x => x.Produto)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status == status);
        }

        var pedidos = await query.OrderByDescending(x => x.DataPedido).ToListAsync();
        return pedidos.Select(ToResponse).ToList();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<PedidoResponse>> GetPedido(int id)
    {
        var pedido = await _context.Pedidos
            .Include(x => x.Cliente)
            .Include(x => x.Itens).ThenInclude(x => x.Produto)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (pedido is null)
        {
            return NotFound();
        }

        return ToResponse(pedido);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<PedidoResponse>> PostPedido(PedidoRequest request)
    {
        if (request.Itens is null || request.Itens.Count == 0)
        {
            return BadRequest("Adicione ao menos um produto ao pedido.");
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

        var produtosPorId = new Dictionary<int, Produto>();
        foreach (var grupo in request.Itens.GroupBy(i => i.ProdutoId))
        {
            var produto = await _context.Produtos.FindAsync(grupo.Key);
            if (produto is null)
            {
                return BadRequest("Selecione um produto válido.");
            }

            var saldoDisponivel = await SaldoDisponivelAsync(produto.Id, produto.Quantidade);
            var quantidadeTotal = grupo.Sum(i => i.Quantidade);
            if (quantidadeTotal > saldoDisponivel)
            {
                return BadRequest($"Estoque insuficiente. Saldo disponível de \"{produto.Descricao}\": {saldoDisponivel}.");
            }

            produtosPorId[grupo.Key] = produto;
        }

        var proximoNumero = await _context.Pedidos.MaxAsync(p => (int?)p.NumeroPedido) ?? 0;

        var pedido = new Pedido
        {
            NumeroPedido = proximoNumero + 1,
            Cliente = cliente,
            DataPedido = request.DataPedido,
            NumeroNota = request.NumeroNota.Trim(),
            FormaPagamento = request.FormaPagamento.Trim(),
            Status = request.Status.Trim(),
            PercentualDesconto = PercentualDescontoEfetivo(request.Status.Trim(), request.PercentualDesconto),
            Observacoes = request.Observacoes.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        foreach (var item in request.Itens)
        {
            pedido.Itens.Add(new PedidoItem
            {
                Produto = produtosPorId[item.ProdutoId],
                Quantidade = item.Quantidade,
                ValorUnitario = item.ValorUnitario
            });
        }

        _context.Pedidos.Add(pedido);
        await _context.SaveChangesAsync();
        await RecalcularUltimaCompraCliente(cliente.Id);

        var pedidoCompleto = await _context.Pedidos
            .Include(x => x.Cliente)
            .Include(x => x.Itens).ThenInclude(x => x.Produto)
            .FirstAsync(x => x.Id == pedido.Id);

        return CreatedAtAction(nameof(GetPedido), new { id = pedido.Id }, ToResponse(pedidoCompleto));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutPedido(int id, PedidoRequest request)
    {
        var existing = await _context.Pedidos
            .Include(x => x.Itens)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        if (request.Itens is null || request.Itens.Count == 0)
        {
            return BadRequest("Adicione ao menos um produto ao pedido.");
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

        var produtosPorId = new Dictionary<int, Produto>();
        foreach (var grupo in request.Itens.GroupBy(i => i.ProdutoId))
        {
            var produto = await _context.Produtos.FindAsync(grupo.Key);
            if (produto is null)
            {
                return BadRequest("Selecione um produto válido.");
            }

            var saldoDisponivel = await SaldoDisponivelAsync(produto.Id, produto.Quantidade, existing.Id);
            var quantidadeTotal = grupo.Sum(i => i.Quantidade);
            if (quantidadeTotal > saldoDisponivel)
            {
                return BadRequest($"Estoque insuficiente. Saldo disponível de \"{produto.Descricao}\": {saldoDisponivel}.");
            }

            produtosPorId[grupo.Key] = produto;
        }

        var clienteAnteriorId = existing.ClienteId;

        _context.PedidoItens.RemoveRange(existing.Itens);
        existing.Itens = request.Itens.Select(item => new PedidoItem
        {
            Produto = produtosPorId[item.ProdutoId],
            Quantidade = item.Quantidade,
            ValorUnitario = item.ValorUnitario
        }).ToList();

        existing.Cliente = cliente;
        existing.DataPedido = request.DataPedido;
        existing.NumeroNota = request.NumeroNota.Trim();
        existing.FormaPagamento = request.FormaPagamento.Trim();
        existing.Status = request.Status.Trim();
        existing.PercentualDesconto = PercentualDescontoEfetivo(existing.Status, request.PercentualDesconto);
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
    public async Task<IActionResult> DeletePedido(int id)
    {
        var pedido = await _context.Pedidos.FirstOrDefaultAsync(x => x.Id == id);
        if (pedido is null)
        {
            return NotFound();
        }

        var clienteId = pedido.ClienteId;

        _context.Pedidos.Remove(pedido);
        await _context.SaveChangesAsync();

        if (clienteId.HasValue)
        {
            await RecalcularUltimaCompraCliente(clienteId.Value);
        }

        return NoContent();
    }

    private async Task<int> SaldoDisponivelAsync(int produtoId, int quantidadeCadastrada, int? excluirPedidoId = null)
    {
        var query = _context.PedidoItens.Where(i => i.ProdutoId == produtoId);
        if (excluirPedidoId.HasValue)
        {
            query = query.Where(i => i.PedidoId != excluirPedidoId.Value);
        }

        var totalPedido = await query.SumAsync(i => (int?)i.Quantidade) ?? 0;
        return quantidadeCadastrada - totalPedido;
    }

    private async Task RecalcularUltimaCompraCliente(int clienteId)
    {
        var cliente = await _context.Clientes.FindAsync(clienteId);
        if (cliente is null)
        {
            return;
        }

        var ultimo = await _context.Pedidos
            .Include(p => p.Itens)
            .Where(p => p.ClienteId == clienteId)
            .OrderByDescending(p => p.DataPedido)
            .FirstOrDefaultAsync();

        cliente.UltimaCompraData = ultimo?.DataPedido;
        cliente.UltimaCompraValor = ultimo is null ? null : CalcularValorComDesconto(ValorTotalPedido(ultimo), ultimo.PercentualDesconto);
        await _context.SaveChangesAsync();
    }

    private static decimal ValorTotalPedido(Pedido pedido)
    {
        return pedido.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
    }

    private static decimal CalcularValorComDesconto(decimal valorTotal, decimal percentualDesconto)
    {
        return Math.Round(valorTotal * (1 - percentualDesconto / 100), 2);
    }

    private static decimal PercentualDescontoEfetivo(string status, decimal percentualDesconto)
    {
        return status is "Devolução" or "Cancelada" ? 0 : percentualDesconto;
    }

    private static PedidoResponse ToResponse(Pedido pedido)
    {
        var itens = pedido.Itens.Select(i => new PedidoItemResponse(
            i.Id,
            i.ProdutoId ?? 0,
            i.Produto?.Descricao ?? "—",
            i.Quantidade,
            i.ValorUnitario,
            i.Quantidade * i.ValorUnitario)).ToList();

        var valorTotal = itens.Sum(i => i.ValorTotal);

        return new PedidoResponse(
            pedido.Id,
            pedido.NumeroPedido,
            pedido.ClienteId ?? 0,
            pedido.Cliente?.Nome ?? "—",
            itens,
            valorTotal,
            pedido.PercentualDesconto,
            CalcularValorComDesconto(valorTotal, pedido.PercentualDesconto),
            pedido.DataPedido,
            pedido.NumeroNota,
            pedido.FormaPagamento,
            pedido.Status,
            pedido.Observacoes,
            pedido.CreatedAt);
    }
}

public sealed record PedidoItemRequest(int ProdutoId, int Quantidade, decimal ValorUnitario);

public sealed record PedidoRequest(
    int ClienteId, List<PedidoItemRequest> Itens, DateTime DataPedido,
    string NumeroNota, string FormaPagamento, string Status, decimal PercentualDesconto, string Observacoes);

public sealed record PedidoItemResponse(int Id, int ProdutoId, string ProdutoNome, int Quantidade, decimal ValorUnitario, decimal ValorTotal);

public sealed record PedidoResponse(
    int Id, int NumeroPedido, int ClienteId, string ClienteNome, List<PedidoItemResponse> Itens,
    decimal ValorTotal, decimal PercentualDesconto, decimal ValorComDesconto, DateTime DataPedido,
    string NumeroNota, string FormaPagamento, string Status, string Observacoes, DateTime CreatedAt);
