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
            var allTx = await _context.Transactions
                .Where(t => t.BatchId == activeBatch.Id)
                .ToListAsync();

            int total = allTx.Count;
            int matched = allTx.Count(t => t.Status == "Reconciled" || t.Status == "Resolved");
            int unmatched = allTx.Count(t => t.Status == "Unmatched");
            int ledgerCount = allTx.Count(t => t.Source == "Ledger");
            int bankCount = allTx.Count(t => t.Source == "Bank");

            ViewBag.UnmatchedCount = unmatched;
            ViewBag.MatchedCount = matched;
            ViewBag.TotalCount = total;
            ViewBag.LedgerCount = ledgerCount;
            ViewBag.BankCount = bankCount;
            ViewBag.MatchedPercent = total > 0 ? Math.Round((double)matched / total * 100, 1) : 0;
        }
        else
        {
            ViewBag.UnmatchedCount = 0;
            ViewBag.MatchedCount = 0;
            ViewBag.TotalCount = 0;
            ViewBag.MatchedPercent = 0;
        }

        return View();
    }
}