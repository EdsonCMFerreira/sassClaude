using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using sassClaude.Data;
using sassClaude.Models;
using sassClaude.Services;

namespace sassClaude.Controllers;

[Authorize]
public class SettingsController : Controller
{
    private readonly SassDbContext _context;
    private readonly PasswordHasher<Login> _passwordHasher = new();
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(SassDbContext context, IEmailSender emailSender, IOptions<EmailOptions> emailOptions, ILogger<SettingsController> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var login = await CurrentLoginAsync();
        if (login is null)
        {
            return Challenge();
        }

        return View(new SettingsPageViewModel
        {
            Profile = new ProfileViewModel { Username = login.Username, Email = login.Email },
            EmailConfirmed = login.EmailConfirmed
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(CancellationToken cancellationToken)
    {
        var login = await CurrentLoginAsync();
        if (login is null)
        {
            return Challenge();
        }

        if (!login.EmailConfirmed)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            _context.EmailVerificationTokens.Add(new EmailVerificationToken
            {
                LoginId = login.Id,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            });
            await _context.SaveChangesAsync(cancellationToken);

            var confirmPath = Url.Action("ConfirmEmail", "Account", new { token = rawToken })!;
            var baseUrl = string.IsNullOrWhiteSpace(_emailOptions.BaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : _emailOptions.BaseUrl.TrimEnd('/');

            try
            {
                await _emailSender.SendEmailConfirmationAsync(login.Email, login.Username, $"{baseUrl}{confirmPath}", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao reenviar e-mail de confirmação para {Email}.", login.Email);
                TempData["SettingsMessage"] = "Não foi possível enviar o e-mail agora. Tente novamente em instantes.";
                return RedirectToAction(nameof(Index));
            }
        }

        TempData["SettingsMessage"] = "E-mail de confirmação reenviado.";
        return RedirectToAction(nameof(Index));
    }

    private static string HashToken(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(SettingsPageViewModel model)
    {
        foreach (var key in ModelState.Keys.Where(key => key.StartsWith("Password.")).ToList())
        {
            ModelState.Remove(key);
        }

        var login = await CurrentLoginAsync();
        if (login is null)
        {
            return Challenge();
        }

        model.EmailConfirmed = login.EmailConfirmed;

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        var username = model.Profile.Username.Trim();
        var email = model.Profile.Email.Trim().ToLowerInvariant();
        var alreadyTaken = await _context.Logins.AnyAsync(item =>
            item.Id != login.Id && (item.Username == username || item.Email == email));
        if (alreadyTaken)
        {
            ModelState.AddModelError("Profile.Username", "Usuário ou e-mail já está em uso.");
            return View("Index", model);
        }

        login.Username = username;
        login.Email = email;
        await _context.SaveChangesAsync();
        await SignInAsync(login);

        TempData["SettingsMessage"] = "Perfil atualizado.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(SettingsPageViewModel model)
    {
        foreach (var key in ModelState.Keys.Where(key => key.StartsWith("Profile.")).ToList())
        {
            ModelState.Remove(key);
        }

        var login = await CurrentLoginAsync();
        if (login is null)
        {
            return Challenge();
        }

        model.Profile = new ProfileViewModel { Username = login.Username, Email = login.Email };
        model.EmailConfirmed = login.EmailConfirmed;

        if (!ModelState.IsValid)
        {
            return View("Index", model);
        }

        if (_passwordHasher.VerifyHashedPassword(login, login.Password, model.Password.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Password.CurrentPassword", "Senha atual incorreta.");
            return View("Index", model);
        }

        login.Password = _passwordHasher.HashPassword(login, model.Password.NewPassword);
        await _context.SaveChangesAsync();

        TempData["SettingsMessage"] = "Senha atualizada.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<Login?> CurrentLoginAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idClaim, out var id) ? await _context.Logins.FindAsync(id) : null;
    }

    private async Task SignInAsync(Login login)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, login.Id.ToString()),
            new Claim(ClaimTypes.Name, login.Username),
            new Claim(ClaimTypes.Email, login.Email),
            new Claim(ClaimTypes.Role, login.Role),
            new Claim(TenantClaimTypes.EmpresaId, login.EmpresaId.ToString()),
            new Claim(TenantClaimTypes.IsSuperAdmin, login.IsSuperAdmin ? "true" : "false")
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
