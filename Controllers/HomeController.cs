using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using AFTRS.Data;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[Authorize] // No one can see the home page without logging in
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;

    public HomeController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Admin"))
        {
            return RedirectToAction("AdminDashboard");
        }
        return RedirectToAction("ManagerDashboard");
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDashboard()
    {
        // Stats for the Admin
        ViewBag.TotalUsers = await _context.Users.CountAsync();
        ViewBag.TotalBatches = await _context.ReconciliationBatches.CountAsync();
        ViewBag.RecentLogs = await _context.SecurityLogs.OrderByDescending(x => x.Timestamp).Take(5).ToListAsync();
        
        return View();
    }

    [Authorize(Roles = "FinancialManager,Admin")]
    public async Task<IActionResult> ManagerDashboard()
    {
        // Stats for the Manager
        var activeBatch = await _context.ReconciliationBatches.FirstOrDefaultAsync(b => !b.IsFinalized);
        ViewBag.ActiveBatch = activeBatch;
        
        if (activeBatch != null)
        {
            ViewBag.UnmatchedCount = await _context.Transactions
                .CountAsync(t => t.BatchId == activeBatch.Id && t.Status == "Unmatched");
        }

        return View();
    }
}