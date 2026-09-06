using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using sassClaude.Data;
using sassClaude.Models;

namespace sassClaude.Controllers;

[Authorize(Roles = "Admin")]
public class LoginController : Controller
{
    private readonly SassDbContext _context;

    public LoginController(SassDbContext context)
    {
        _context = context;
    }

    public IActionResult Index()
    {
        return View();
    }
}

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/login")]
public class ApiLoginController : ControllerBase
{
    private static readonly string[] ValidRoles = ["Admin", "Colaborador"];

    private readonly SassDbContext _context;
    private readonly PasswordHasher<Login> _passwordHasher = new();

    public ApiLoginController(SassDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<LoginResponse>>> GetLogins()
    {
        return await _context.Logins
            .OrderByDescending(x => x.CreatedAt)
            .Select(login => new LoginResponse(login.Id, login.Username, login.Email, login.Role, login.CreatedAt))
            .ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LoginResponse>> GetLogin(int id)
    {
        var login = await _context.Logins.FindAsync(id);
        if (login is null)
        {
            return NotFound();
        }

        return new LoginResponse(login.Id, login.Username, login.Email, login.Role, login.CreatedAt);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<LoginResponse>> PostLogin(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Dados de login inválidos.");
        }

        var role = ValidRoles.Contains(request.Role) ? request.Role : "Colaborador";

        var login = new Login
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim(),
            Role = role,
            CreatedAt = DateTime.UtcNow
        };
        login.Password = _passwordHasher.HashPassword(login, request.Password);
        _context.Logins.Add(login);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetLogin), new { id = login.Id },
            new LoginResponse(login.Id, login.Username, login.Email, login.Role, login.CreatedAt));
    }

    [HttpPut("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PutLogin(int id, LoginRequest request)
    {
        var existing = await _context.Logins.FindAsync(id);
        if (existing is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Usuário e e-mail são obrigatórios.");
        }

        var role = ValidRoles.Contains(request.Role) ? request.Role : existing.Role;
        if (existing.Role == "Admin" && role != "Admin" && await IsLastAdmin(existing.Id))
        {
            return BadRequest("Não é possível remover o último administrador.");
        }

        existing.Username = request.Username.Trim();
        existing.Email = request.Email.Trim();
        existing.Role = role;
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            existing.Password = _passwordHasher.HashPassword(existing, request.Password);
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteLogin(int id)
    {
        var login = await _context.Logins.FindAsync(id);
        if (login is null)
        {
            return NotFound();
        }

        if (login.Role == "Admin" && await IsLastAdmin(login.Id))
        {
            return BadRequest("Não é possível excluir o último administrador.");
        }

        _context.Logins.Remove(login);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<bool> IsLastAdmin(int excludingId)
    {
        return !await _context.Logins.AnyAsync(item => item.Role == "Admin" && item.Id != excludingId);
    }
}

public sealed record LoginRequest(string Username, string Password, string Email, string Role);

public sealed record LoginResponse(int Id, string Username, string Email, string Role, DateTime CreatedAt);
