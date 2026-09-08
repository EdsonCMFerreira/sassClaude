using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using sassClaude.Data;
using sassClaude.Models;
using sassClaude.Services;

namespace sassClaude.Controllers;

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

    public IActionResult Index() => View();

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
