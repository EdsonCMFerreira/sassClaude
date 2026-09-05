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

    public AccountController(SassDbContext context, IEmailSender emailSender, IOptions<EmailOptions> emailOptions)
    {
        _context = context;
        _emailSender = emailSender;
        _emailOptions = emailOptions.Value;
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
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new RegisterViewModel());
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
    public async Task<IActionResult> Register(RegisterViewModel model)
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
        await _context.SaveChangesAsync();
        await SignIn(login);

        return RedirectToAction("Index", "Dashboard");
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
