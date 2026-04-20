using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace AFTRS.Controllers;

[Authorize]
public class HelpController : Controller
{
    // SRS 3.6: Help page explaining correct CSV/Excel upload format
    public IActionResult Index() => View();
}
