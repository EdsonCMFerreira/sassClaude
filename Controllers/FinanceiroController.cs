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
        var pedidos = await _context.Pedidos.Include(p => p.Itens).Where(p => p.Status == "Concluída").ToListAsync();

        var hoje = DateTime.UtcNow.Date;
        var meses = Enumerable.Range(0, 6)
            .Select(i => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i))
            .OrderBy(d => d)
            .Select(mes => new DreRow(
                mes,
                pedidos.Where(p => p.DataPedido.Year == mes.Year && p.DataPedido.Month == mes.Month).Sum(ValorLiquido)))
            .ToList();

        var model = new FinanceiroViewModel
        {
            TotalEntradas = pedidos.Sum(ValorLiquido),
            Meses = meses
        };

        return View(model);
    }

    private static decimal ValorLiquido(Pedido pedido)
    {
        var valorTotal = pedido.Itens.Sum(i => i.Quantidade * i.ValorUnitario);
        return Math.Round(valorTotal * (1 - pedido.PercentualDesconto / 100), 2);
    }
}
