using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;

namespace Saas.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogController : Controller
{
    public IActionResult Index(string? entidade)
    {
        ViewData["EntidadeSelecionada"] = entidade ?? string.Empty;
        return View();
    }
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/auditlog")]
public class ApiAuditLogController : ControllerBase
{
    private readonly SassDbContext _context;

    public ApiAuditLogController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AuditLogEntryResponse>>> GetEntradas([FromQuery] string? entidade)
    {
        var query = _context.AuditLogEntries.AsQueryable();
        if (!string.IsNullOrWhiteSpace(entidade))
        {
            query = query.Where(x => x.EntityName == entidade);
        }

        var entradas = await query
            .OrderByDescending(x => x.Timestamp)
            .Take(2000)
            .Select(x => new AuditLogEntryResponse(x.Timestamp, x.EntityName, x.EntityId, x.Action, x.UserName, x.Details))
            .ToListAsync();

        return entradas;
    }
}

public sealed record AuditLogEntryResponse(DateTime Timestamp, string EntityName, int EntityId, string Action, string UserName, string Details);
