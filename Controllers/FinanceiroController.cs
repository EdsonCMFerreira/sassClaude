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
        var compras = await _context.Compras.Where(c => c.Status == "Concluída").ToListAsync();

        var hoje = DateTime.UtcNow.Date;
        var meses = Enumerable.Range(0, 6)
            .Select(i => new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-i))
            .OrderBy(d => d)
            .Select(mes => new DreRow(
                mes,
                vendas.Where(v => v.DataVenda.Year == mes.Year && v.DataVenda.Month == mes.Month).Sum(v => v.Quantidade * v.ValorUnitario),
                compras.Where(c => c.DataCompra.Year == mes.Year && c.DataCompra.Month == mes.Month).Sum(c => c.Quantidade * c.ValorUnitario),
                0m))
            .Select(row => row with { Resultado = row.Entradas - row.Saidas })
            .ToList();

        var totalEntradas = vendas.Sum(v => v.Quantidade * v.ValorUnitario);
        var totalSaidas = compras.Sum(c => c.Quantidade * c.ValorUnitario);

        var model = new FinanceiroViewModel
        {
            TotalEntradas = totalEntradas,
            TotalSaidas = totalSaidas,
            ResultadoTotal = totalEntradas - totalSaidas,
            Meses = meses
        };

        return View(model);
    }
}
