using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Saas.Data;
using Saas.Models;
using Saas.Services;

namespace Saas.Controllers;

[Authorize]
public class WorkspaceController : Controller
{
    private readonly SassDbContext _context;
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<WorkspaceController> _logger;

    public WorkspaceController(SassDbContext context, IEmailSender emailSender, IOptions<EmailOptions> emailOptions, ILogger<WorkspaceController> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var currentId = int.TryParse(idClaim, out var parsed) ? parsed : 0;

        var membros = await _context.Logins
            .OrderByDescending(item => item.Role == "Admin")
            .ThenBy(item => item.Username)
            .Select(item => new WorkspaceMemberRow(item.Id, item.Username, item.Email, item.Role, item.CreatedAt, item.Id == currentId))
            .ToListAsync();

        return View(new WorkspaceIndexPageViewModel { Membros = membros, PodeGerenciar = User.IsInRole("Admin") });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AlterarPapel(int id)
    {
        var login = await _context.Logins.FindAsync(id);
        if (login is null)
        {
            return NotFound();
        }

        var novoPapel = login.Role == "Admin" ? "Colaborador" : "Admin";
        if (login.Role == "Admin" && novoPapel != "Admin" && await IsLastAdmin(login.Id))
        {
            TempData["WorkspaceMessage"] = "Não é possível remover o papel do último administrador.";
            return RedirectToAction(nameof(Index));
        }

        login.Role = novoPapel;
        await _context.SaveChangesAsync();
        TempData["WorkspaceMessage"] = $"{login.Username} agora é {novoPapel}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoverMembro(int id)
    {
        var login = await _context.Logins.FindAsync(id);
        if (login is null)
        {
            return NotFound();
        }

        if (login.Role == "Admin" && await IsLastAdmin(login.Id))
        {
            TempData["WorkspaceMessage"] = "Não é possível remover o último administrador.";
            return RedirectToAction(nameof(Index));
        }

        _context.Logins.Remove(login);
        await _context.SaveChangesAsync();
        TempData["WorkspaceMessage"] = $"{login.Username} foi removido do workspace.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> IsLastAdmin(int excludingId)
    {
        return !await _context.Logins.AnyAsync(item => item.Role == "Admin" && item.Id != excludingId);
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Invite()
    {
        return View(new WorkspaceInvitePageViewModel { Invites = await LoadInvites() });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(WorkspaceInvitePageViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.Invites = await LoadInvites();
            return View(model);
        }

        var email = model.Form.Email.Trim().ToLowerInvariant();
        var alreadyMember = await _context.Logins.IgnoreQueryFilters().AnyAsync(item => item.Email == email, cancellationToken);
        if (alreadyMember)
        {
            ModelState.AddModelError("Form.Email", "Essa pessoa já faz parte do workspace.");
            model.Invites = await LoadInvites();
            return View(model);
        }

        var pending = await _context.WorkspaceInvites
            .AnyAsync(item => item.Email == email && item.AcceptedAt == null && item.ExpiresAt > DateTime.UtcNow, cancellationToken);
        if (pending)
        {
            ModelState.AddModelError("Form.Email", "Já existe um convite pendente para esse e-mail.");
            model.Invites = await LoadInvites();
            return View(model);
        }

        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(idClaim, out var inviterId))
        {
            return Challenge();
        }

        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        _context.WorkspaceInvites.Add(new WorkspaceInvite
        {
            Email = email,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken))),
            InvitedByLoginId = inviterId,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _context.SaveChangesAsync(cancellationToken);

        var acceptPath = Url.Action("Register", "Account", new { invite = rawToken })!;
        var baseUrl = string.IsNullOrWhiteSpace(_emailOptions.BaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : _emailOptions.BaseUrl.TrimEnd('/');

        try
        {
            await _emailSender.SendWorkspaceInviteAsync(email, User.Identity?.Name ?? "Um colega", $"{baseUrl}{acceptPath}", cancellationToken);
            TempData["SettingsMessage"] = "Convite enviado.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar convite para {Email}.", email);
            TempData["SettingsMessage"] = "O convite foi criado, mas o e-mail não pôde ser enviado agora.";
        }

        return RedirectToAction(nameof(Invite));
    }

    private async Task<List<WorkspaceInviteRow>> LoadInvites()
    {
        return await _context.WorkspaceInvites
            .OrderByDescending(item => item.CreatedAt)
            .Select(item => new WorkspaceInviteRow(item.Email, item.CreatedAt, item.ExpiresAt, item.AcceptedAt != null))
            .ToListAsync();
    }
}
