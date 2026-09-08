using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize(Policy = "SuperAdmin")]
public class SuperAdminController : Controller
{
    private static readonly string[] AllowedPlanos = ["Starter", "Pro"];

    private readonly SassDbContext _context;

    public SuperAdminController(SassDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var empresas = await _context.Empresas.OrderBy(e => e.Nome).ToListAsync();

        var loginCounts = await CountsByEmpresa(_context.Logins);
        var produtoCounts = await CountsByEmpresa(_context.Produtos);
        var clienteCounts = await CountsByEmpresa(_context.Clientes);
        var pedidoCounts = await CountsByEmpresa(_context.Pedidos);

        var rows = empresas.Select(e => new EmpresaSummaryRow(
            e.Id, e.Nome, e.Plano, e.CreatedAt,
            loginCounts.GetValueOrDefault(e.Id),
            produtoCounts.GetValueOrDefault(e.Id),
            clienteCounts.GetValueOrDefault(e.Id),
            pedidoCounts.GetValueOrDefault(e.Id))).ToList();

        return View(new SuperAdminViewModel { Empresas = rows, AllowedPlanos = AllowedPlanos });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPlano(int empresaId, string plano)
    {
        if (!AllowedPlanos.Contains(plano))
        {
            TempData["SuperAdminMessage"] = "Plano inválido.";
            return RedirectToAction(nameof(Index));
        }

        var empresa = await _context.Empresas.FindAsync(empresaId);
        if (empresa is null)
        {
            TempData["SuperAdminMessage"] = "Empresa não encontrada.";
            return RedirectToAction(nameof(Index));
        }

        empresa.Plano = plano;
        await _context.SaveChangesAsync();
        TempData["SuperAdminMessage"] = $"Plano de {empresa.Nome} atualizado para {plano}.";
        return RedirectToAction(nameof(Index));
    }

    private static async Task<Dictionary<int, int>> CountsByEmpresa<T>(IQueryable<T> set) where T : class, ITenantScoped
    {
        return await set.IgnoreQueryFilters()
            .GroupBy(x => x.EmpresaId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
