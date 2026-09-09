using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Saas.Data;

namespace Saas.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly SassDbContext _context;

    public DashboardController(SassDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var totalUsers = await _context.Logins.CountAsync();
        return View(new DashboardViewModel(totalUsers));
    }
}

public sealed record DashboardViewModel(int TotalUsers);
