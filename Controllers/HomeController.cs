using AFTRS.Data;
using AFTRS.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Index()
    {
        if (User?.Identity?.IsAuthenticated != true)
            return RedirectToAction("Login", "Account");

        var role = User.FindFirst(AuthConstants.RoleClaimType)?.Value;
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction(nameof(AdminDashboard));
        return RedirectToAction(nameof(ManagerDashboard));
    }

    [RoleAuthorize("Admin")]
    [HttpGet]
    public async Task<IActionResult> AdminDashboard()
    {
        ViewBag.TotalUsers = await _context.Users.CountAsync();
        ViewBag.RecentSecurityLogs = await _context.SecurityLogs
            .Include(l => l.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(10)
            .ToListAsync();

        return View();
    }

    [RoleAuthorize("Manager", "Admin")]
    [HttpGet]
    public async Task<IActionResult> ManagerDashboard()
    {
        var all = await _context.Transactions.ToListAsync();

        ViewBag.TotalCount = all.Count;
        ViewBag.ReconciledCount = all.Count(t => t.Status == "Reconciled");
        ViewBag.UnmatchedCount = all.Count(t => t.Status == "Unmatched");
        ViewBag.LedgerCount = all.Count(t => t.Source == "Ledger");
        ViewBag.BankCount = all.Count(t => t.Source == "Bank");

        var total = (int)ViewBag.TotalCount;
        var reconciled = (int)ViewBag.ReconciledCount;
        ViewBag.MatchPercent = total > 0 ? Math.Round((double)reconciled / total * 100, 1) : 0;

        return View();
    }
}
