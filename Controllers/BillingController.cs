using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize]
public class BillingController : Controller
{
    private readonly SassDbContext _context;

    public BillingController(SassDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var invoices = int.TryParse(idClaim, out var id)
            ? await _context.Invoices
                .Where(item => item.LoginId == id)
                .OrderByDescending(item => item.IssuedAt)
                .ToListAsync()
            : new List<Invoice>();

        return View(invoices);
    }
}
