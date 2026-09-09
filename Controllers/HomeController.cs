using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Saas.Models;
using Saas.Services;

namespace Saas.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IEmailSender _emailSender;

    public HomeController(ILogger<HomeController> logger, IEmailSender emailSender)
    {
        _logger = logger;
        _emailSender = emailSender;
    }

    public IActionResult Index()
    {
        return User.Identity?.IsAuthenticated == true
            ? RedirectToAction("Index", "Dashboard")
            : View();
    }

    public IActionResult Pricing() => View();

    public IActionResult Terms() => View();

    public IActionResult Privacy() => View();

    public IActionResult Help() => View();

    public IActionResult About() => View(new ContactMessageViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Contact(ContactMessageViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("About", model);
        }

        try
        {
            await _emailSender.SendContactMessageAsync(model.Name.Trim(), model.Email.Trim(), model.Message.Trim(), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao enviar mensagem de contato de {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "Não foi possível enviar sua mensagem agora. Tente novamente ou escreva para suporte@saas.com.");
            return View("About", model);
        }

        TempData["ContactSent"] = true;
        return RedirectToAction(nameof(About));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult PageNotFound() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
