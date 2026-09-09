using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Saas.Controllers;

[Authorize]
public class PesquisaController : Controller
{
    public IActionResult Index() => View();
}
