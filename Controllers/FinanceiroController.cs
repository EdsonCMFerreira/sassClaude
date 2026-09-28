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
            .Include(p => p.Itens).ThenInclude(i => i.Produto)
            .ToListAsync();

        var todasCompras = await _context.Compras
            .Include(c => c.Fornecedor)
            .Include(c => c.Itens)
            .ToListAsync();

        var concluidos = todosPedidos.Where(p => p.Status == "Concluída").ToList();

        var hoje = DateTime.UtcNow.Date;
        var meses = Enumerable.Range(0, 6)
            .Select(i => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i))
            .OrderBy(d => d)
            .Select(mes =>
            {
                var doMes = concluidos.Where(p => p.DataPedido.Year == mes.Year && p.DataPedido.Month == mes.Month).ToList();
                var receitaMes = doMes.Sum(ValorLiquido);
                var custoMes = doMes.Sum(CustoTotal);
                return new DreRow(mes, receitaMes, custoMes, receitaMes - custoMes);
            })
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

        var contasAPagar = todasCompras
            .Where(c => c.Status != "Cancelada" && !c.DataPagamento.HasValue)
            .Select(c =>
            {
                int? dias = c.DataVencimento.HasValue ? (int)(c.DataVencimento.Value.Date - hoje).TotalDays : null;
                var situacao = dias switch
                {
                    null => "Sem vencimento",
                    < 0 => "Vencido",
                    <= 7 => "Vence em breve",
                    _ => "Em dia"
                };
                return new ContaPagarRow(c.Id, c.NumeroCompra, c.Fornecedor?.Nome ?? "—", ValorCompra(c), c.DataVencimento, dias, situacao);
            })
            .OrderBy(r => r.DataVencimento ?? DateTime.MaxValue)
            .ToList();

        var fluxoCaixa = new List<FluxoCaixaRow>();
        var saldoAcumulado = 0m;
        for (var i = 0; i < 6; i++)
        {
            var mes = new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(i);
            var entradasMes = contasAReceber
                .Where(r => r.DataVencimento.HasValue && r.DataVencimento.Value.Year == mes.Year && r.DataVencimento.Value.Month == mes.Month)
                .Sum(r => r.Valor);
            var saidasMes = contasAPagar
                .Where(r => r.DataVencimento.HasValue && r.DataVencimento.Value.Year == mes.Year && r.DataVencimento.Value.Month == mes.Month)
                .Sum(r => r.Valor);
            var saldoMes = entradasMes - saidasMes;
            saldoAcumulado += saldoMes;
            fluxoCaixa.Add(new FluxoCaixaRow(mes, entradasMes, saidasMes, saldoMes, saldoAcumulado));
        }

        var vendasPorFormaPagamento = concluidos
            .GroupBy(p => string.IsNullOrWhiteSpace(p.FormaPagamento) ? "Não informado" : p.FormaPagamento)
            .Select(g => new FormaPagamentoRow(g.Key, g.Sum(ValorLiquido), g.Count()))
            .OrderByDescending(r => r.Total)
            .ToList();

        var totalReceita = concluidos.Sum(ValorLiquido);
        var totalCusto = concluidos.Sum(CustoTotal);
        var lucroBruto = totalReceita - totalCusto;

        var model = new FinanceiroViewModel
        {
            TotalEntradas = totalReceita,
            Meses = meses,
            TotalEmAberto = contasAReceber.Sum(r => r.Valor),
            TotalVencido = contasAReceber.Where(r => r.Situacao == "Vencido").Sum(r => r.Valor),
            TotalVenceEm7Dias = contasAReceber.Where(r => r.Situacao == "Vence em breve").Sum(r => r.Valor),
            QuantidadeEmAberto = contasAReceber.Count,
            ContasAReceber = contasAReceber,
            TotalEmAbertoPagar = contasAPagar.Sum(r => r.Valor),
            TotalVencidoPagar = contasAPagar.Where(r => r.Situacao == "Vencido").Sum(r => r.Valor),
            TotalVenceEm7DiasPagar = contasAPagar.Where(r => r.Situacao == "Vence em breve").Sum(r => r.Valor),
            QuantidadeEmAbertoPagar = contasAPagar.Count,
            ContasAPagar = contasAPagar,
            FluxoCaixa = fluxoCaixa,
            VendasPorFormaPagamento = vendasPorFormaPagamento,
            TotalCusto = totalCusto,
            LucroBruto = lucroBruto,
            MargemBrutaPercentual = totalReceita > 0 ? Math.Round(lucroBruto / totalReceita * 100, 1) : 0
        };

        return View(model);
    }

    private static decimal ValorLiquido(Pedido pedido)
    {
        var valorTotal = pedido.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
        return Math.Round(valorTotal * (1 - pedido.PercentualDesconto / 100), 2);
    }

    private static decimal CustoTotal(Pedido pedido)
    {
        return pedido.Itens.Sum(i => i.Quantidade * (i.Produto?.ValorCompra ?? 0));
    }

    private static decimal ValorCompra(Compra compra)
    {
        return compra.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
    }
}
