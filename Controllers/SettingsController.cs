using AFTRS.Data;
using AFTRS.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public SettingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await GetLoggedInUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        ViewBag.Theme = UiText.Theme(Request);
        ViewBag.Language = UiText.Language(Request);
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult UpdatePreferences(string theme, string language)
    {
        var normalizedTheme = string.Equals(theme, "dark", StringComparison.OrdinalIgnoreCase) ? "dark" : "light";
        var normalizedLanguage = string.Equals(language, "ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
        var options = new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps
        };

        Response.Cookies.Append("AFTRS.Theme", normalizedTheme, options);
        Response.Cookies.Append("AFTRS.Language", normalizedLanguage, options);
        TempData["Msg"] = UiText.T(normalizedLanguage, "SettingsSaved");

        return RedirectToAction(nameof(Index));
    }

    private async Task<Models.User?> GetLoggedInUserAsync()
    {
        var value = User.FindFirst(AuthConstants.UserIdClaimType)?.Value;
        if (!int.TryParse(value, out var userId)) return null;
        return await _context.Users.FirstOrDefaultAsync(u => u.UserID == userId);
    }
}
