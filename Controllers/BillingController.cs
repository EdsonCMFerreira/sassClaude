using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;
using Saas.Models;
using Saas.Services;

namespace Saas.Controllers;

[Authorize(Roles = "Admin")]
public class BillingController : Controller
{
    private readonly SassDbContext _context;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;

    public BillingController(SassDbContext context, ICurrentTenantAccessor currentTenantAccessor)
    {
        _context = context;
        _currentTenantAccessor = currentTenantAccessor;
    }

    public async Task<IActionResult> Index()
    {
        var empresa = _currentTenantAccessor.EmpresaId.HasValue
            ? await _context.Empresas.FindAsync(_currentTenantAccessor.EmpresaId.Value)
            : null;

        var invoices = await _context.Invoices
            .OrderByDescending(item => item.IssuedAt)
            .ToListAsync();

        return View(new BillingViewModel { Plano = empresa?.Plano ?? "Starter", Invoices = invoices });
    }
}
