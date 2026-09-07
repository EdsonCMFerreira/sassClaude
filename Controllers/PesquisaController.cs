using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace sassClaude.Controllers;

[Authorize]
public class PesquisaController : Controller
{
    public IActionResult Index() => View();
}
