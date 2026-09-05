using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace sassClaude.Controllers;

[Authorize]
public class SettingsController : Controller
{
    public IActionResult Index() => View();
}
