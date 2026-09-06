using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using sassClaude.Data;
using sassClaude.Models;
using sassClaude.Services;

namespace sassClaude.Controllers;

public class AccountController : Controller
{
    private readonly SassDbContext _context;
    private readonly PasswordHasher<Login> _passwordHasher = new();
    private readonly IEmailSender _emailSender;
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<AccountController> _logger;

    public AccountController(SassDbContext context, IEmailSender emailSender, IOptions<EmailOptions> emailOptions, ILogger<AccountController> logger)
    {
        _context = context;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var identifier = model.Identifier.Trim();
        var login = await _context.Logins.FirstOrDefaultAsync(item =>
            item.Username == identifier || item.Email == identifier);

        if (login is null || _passwordHasher.VerifyHashedPassword(login, login.Password, model.Password)
            == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Usuário ou senha inválidos.");
            return View(model);
        }

        await SignIn(login);
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> Register(string? invite = null, CancellationToken cancellationToken = default)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var model = new RegisterViewModel();
        if (!string.IsNullOrWhiteSpace(invite))
        {
            var pendingInvite = await FindValidInvite(invite, cancellationToken);
            if (pendingInvite is not null)
            {
                model.Email = pendingInvite.Email;
                model.InviteToken = invite;
            }
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var email = model.Email.Trim().ToLowerInvariant();
        var login = await _context.Logins.FirstOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (login is not null)
        {
            var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
            var token = new PasswordResetToken
            {
                LoginId = login.Id,
                TokenHash = HashToken(rawToken),
                ExpiresAt = DateTime.UtcNow.AddMinutes(30)
            };

            var oldTokens = _context.PasswordResetTokens.Where(item => item.LoginId == login.Id && item.UsedAt == null);
            _context.PasswordResetTokens.RemoveRange(oldTokens);
            _context.PasswordResetTokens.Add(token);
            await _context.SaveChangesAsync(cancellationToken);

            var resetPath = Url.Action(nameof(ResetPassword), "Account", new { token = rawToken })!;
            var baseUrl = string.IsNullOrWhiteSpace(_emailOptions.BaseUrl)
                ? $"{Request.Scheme}://{Request.Host}"
                : _emailOptions.BaseUrl.TrimEnd('/');
            var resetUrl = $"{baseUrl}{resetPath}";
            await _emailSender.SendPasswordResetAsync(login.Email, login.Username, resetUrl, cancellationToken);
            if (!_emailOptions.IsConfigured && HttpContext.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                TempData["DevelopmentResetUrl"] = resetUrl;
            }
        }

        return View("ForgotPasswordSent");
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token, CancellationToken cancellationToken)
    {
        var resetToken = await FindValidToken(token, cancellationToken);
        if (resetToken is null)
        {
            return View("ResetPasswordInvalid");
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resetToken = await FindValidToken(model.Token, cancellationToken);
        if (resetToken is null)
        {
            return View("ResetPasswordInvalid");
        }

        resetToken.Login.Password = _passwordHasher.HashPassword(resetToken.Login, model.Password);
        resetToken.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return View("ResetPasswordSuccess");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var username = model.Username.Trim();
        var email = model.Email.Trim().ToLowerInvariant();
        var alreadyExists = await _context.Logins.AnyAsync(item =>
            item.Username == username || item.Email == email);

        if (alreadyExists)
        {
            ModelState.AddModelError(string.Empty, "Usuário ou e-mail já cadastrado.");
            return View(model);
        }

        var login = new Login
        {
            Username = username,
            Email = email,
            CreatedAt = DateTime.UtcNow
        };
        login.Password = _passwordHasher.HashPassword(login, model.Password);

        _context.Logins.Add(login);
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(model.InviteToken))
        {
            var pendingInvite = await FindValidInvite(model.InviteToken, cancellationToken);
            if (pendingInvite is not null)
            {
                pendingInvite.AcceptedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        try
        {
            await SendEmailConfirmation(login, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar e-mail de confirmação para {Email}.", login.Email);
        }

        await SignIn(login);

        return RedirectToAction("Index", "Dashboard");
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string token, CancellationToken cancellationToken)
    {
        var verification = await FindValidVerificationToken(token, cancellationToken);
        if (verification is null)
        {
            return View("EmailConfirmationInvalid");
        }

        verification.Login.EmailConfirmed = true;
        verification.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return View("EmailConfirmed");
    }

    private async Task SendEmailConfirmation(Login login, CancellationToken cancellationToken)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        _context.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            LoginId = login.Id,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });
        await _context.SaveChangesAsync(cancellationToken);

        var confirmPath = Url.Action(nameof(ConfirmEmail), "Account", new { token = rawToken })!;
        var baseUrl = string.IsNullOrWhiteSpace(_emailOptions.BaseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : _emailOptions.BaseUrl.TrimEnd('/');
        var confirmUrl = $"{baseUrl}{confirmPath}";
        await _emailSender.SendEmailConfirmationAsync(login.Email, login.Username, confirmUrl, cancellationToken);
    }

    private async Task<WorkspaceInvite?> FindValidInvite(string rawToken, CancellationToken cancellationToken)
    {
        var invite = await _context.WorkspaceInvites
            .SingleOrDefaultAsync(item => item.TokenHash == HashToken(rawToken) && item.AcceptedAt == null, cancellationToken);
        return invite is not null && invite.ExpiresAt > DateTime.UtcNow ? invite : null;
    }

    private async Task<EmailVerificationToken?> FindValidVerificationToken(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var token = await _context.EmailVerificationTokens
            .Include(item => item.Login)
            .SingleOrDefaultAsync(item => item.TokenHash == HashToken(rawToken) && item.UsedAt == null, cancellationToken);
        return token is not null && token.ExpiresAt > DateTime.UtcNow ? token : null;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task SignIn(Login login)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, login.Id.ToString()),
            new Claim(ClaimTypes.Name, login.Username),
            new Claim(ClaimTypes.Email, login.Email)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }

    private async Task<PasswordResetToken?> FindValidToken(string rawToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return null;
        }

        var token = await _context.PasswordResetTokens
            .Include(item => item.Login)
            .SingleOrDefaultAsync(item => item.TokenHash == HashToken(rawToken) && item.UsedAt == null, cancellationToken);
        return token is not null && token.ExpiresAt > DateTime.UtcNow ? token : null;
    }

    private static string HashToken(string rawToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
    }
}
