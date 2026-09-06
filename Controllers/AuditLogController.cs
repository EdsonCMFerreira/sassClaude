using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;

namespace sassClaude.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogController : Controller
{
    private readonly SassDbContext _context;

    public AuditLogController(SassDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? entidade)
    {
        var query = _context.AuditLogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entidade))
        {
            query = query.Where(x => x.EntityName == entidade);
        }

        var entries = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(200)
            .ToListAsync();

        ViewData["EntidadeSelecionada"] = entidade ?? string.Empty;
        return View(entries);
    }
}
