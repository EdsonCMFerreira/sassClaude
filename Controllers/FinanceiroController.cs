using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

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
        var vendas = await _context.Vendas.Where(v => v.Status == "Concluída").ToListAsync();

        var hoje = DateTime.UtcNow.Date;
        var meses = Enumerable.Range(0, 6)
            .Select(i => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i))
            .OrderBy(d => d)
            .Select(mes => new DreRow(
                mes,
                vendas.Where(v => v.DataVenda.Year == mes.Year && v.DataVenda.Month == mes.Month).Sum(ValorLiquido)))
            .ToList();

        var model = new FinanceiroViewModel
        {
            TotalEntradas = vendas.Sum(ValorLiquido),
            Meses = meses
        };

        return View(model);
    }

    private static decimal ValorLiquido(Venda venda)
    {
        var valorTotal = venda.Quantidade * venda.ValorUnitario;
        return Math.Round(valorTotal * (1 - venda.PercentualDesconto / 100), 2);
    }
}
