using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;
using Saas.Models;

namespace Saas.Controllers;

[Authorize(Roles = "Admin")]
public class FinanceiroController : Controller
{
    private readonly SassDbContext _context;

    public FinanceiroController(SassDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var todosPedidos = await _context.Pedidos
            .Include(p => p.Cliente)
            .Include(p => p.Itens)
            .ToListAsync();

        var concluidos = todosPedidos.Where(p => p.Status == "Concluída").ToList();

        var hoje = DateTime.UtcNow.Date;
        var meses = Enumerable.Range(0, 6)
            .Select(i => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i))
            .OrderBy(d => d)
            .Select(mes => new DreRow(
                mes,
                concluidos.Where(p => p.DataPedido.Year == mes.Year && p.DataPedido.Month == mes.Month).Sum(ValorLiquido)))
            .ToList();

        var contasAReceber = todosPedidos
            .Where(p => p.Status != "Cancelada" && p.Status != "Devolução" && !p.DataPagamento.HasValue)
            .Select(p =>
            {
                int? dias = p.DataVencimento.HasValue ? (int)(p.DataVencimento.Value.Date - hoje).TotalDays : null;
                var situacao = dias switch
                {
                    null => "Sem vencimento",
                    < 0 => "Vencido",
                    <= 7 => "Vence em breve",
                    _ => "Em dia"
                };
                return new ContaReceberRow(p.Id, p.NumeroPedido, p.Cliente?.Nome ?? "—", ValorLiquido(p), p.DataVencimento, dias, situacao);
            })
            .OrderBy(r => r.DataVencimento ?? DateTime.MaxValue)
            .ToList();

        var vendasPorFormaPagamento = concluidos
            .GroupBy(p => string.IsNullOrWhiteSpace(p.FormaPagamento) ? "Não informado" : p.FormaPagamento)
            .Select(g => new FormaPagamentoRow(g.Key, g.Sum(ValorLiquido), g.Count()))
            .OrderByDescending(r => r.Total)
            .ToList();

        var model = new FinanceiroViewModel
        {
            TotalEntradas = concluidos.Sum(ValorLiquido),
            Meses = meses,
            TotalEmAberto = contasAReceber.Sum(r => r.Valor),
            TotalVencido = contasAReceber.Where(r => r.Situacao == "Vencido").Sum(r => r.Valor),
            TotalVenceEm7Dias = contasAReceber.Where(r => r.Situacao == "Vence em breve").Sum(r => r.Valor),
            QuantidadeEmAberto = contasAReceber.Count,
            ContasAReceber = contasAReceber,
            VendasPorFormaPagamento = vendasPorFormaPagamento
        };

        return View(model);
    }

    private static decimal ValorLiquido(Pedido pedido)
    {
        var valorTotal = pedido.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
        return Math.Round(valorTotal * (1 - pedido.PercentualDesconto / 100), 2);
    }
}
