using Microsoft.AspNetCore.Mvc;

namespace AFTRS.Controllers;

public class HelpController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
