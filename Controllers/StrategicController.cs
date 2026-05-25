using AFTRS.Data;
using AFTRS.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
public class StrategicController : Controller
{
    private readonly ApplicationDbContext _context;

    public StrategicController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

        var now = DateTime.Today;
        var budgets = await _context.BudgetTargets
            .Include(b => b.Category)
            .Where(b => b.TargetMonth == now.Month && b.TargetYear == now.Year)
            .ToListAsync();

        // Actual spending: ledger totals per category for current month.
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var actuals = await _context.Transactions
            .Where(t => t.Source == "Ledger" && t.CategoryID != null && t.TransactionDate >= monthStart && t.TransactionDate < monthEnd)
            .GroupBy(t => t.CategoryID)
            .Select(g => new { CategoryID = g.Key!.Value, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.CategoryID, x => x.Total);

        ViewBag.ActualSpending = actuals;
        return View(budgets);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetBudget(int categoryId, int targetMonth, int targetYear, decimal targetAmount)
    {
        if (targetMonth < 1 || targetMonth > 12) return BadRequest("Invalid month.");
        if (targetYear < 2000 || targetYear > 2100) return BadRequest("Invalid year.");

        var existing = await _context.BudgetTargets
            .FirstOrDefaultAsync(b => b.CategoryID == categoryId && b.TargetMonth == targetMonth && b.TargetYear == targetYear);

        if (existing == null)
        {
            _context.BudgetTargets.Add(new Models.BudgetTarget
            {
                CategoryID = categoryId,
                TargetMonth = targetMonth,
                TargetYear = targetYear,
                TargetAmount = decimal.Round(targetAmount, 2)
            });
        }
        else
        {
            // Budget targets aren't audit-logged in the SRS; allow update.
            existing.TargetAmount = decimal.Round(targetAmount, 2);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // FR: Cash Flow projection based on average spending over past 3 months, project next 3.
    [HttpGet]
    public async Task<IActionResult> GetCashFlowData()
    {
        var today = DateTime.Today;
        var start = today.AddMonths(-3);
        var tx = await _context.Transactions
            .Where(t => t.Source == "Ledger" && t.TransactionDate >= start)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();

        // Monthly totals for past 3 months (including current partial).
        var past = tx
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.Amount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        decimal avg = past.Count > 0 ? past.Average(x => x.Total) : 0m;
        var projected = new List<object>();
        for (int i = 1; i <= 3; i++)
        {
            var d = new DateTime(today.Year, today.Month, 1).AddMonths(i);
            projected.Add(new { Year = d.Year, Month = d.Month, Total = decimal.Round(avg, 2) });
        }

        return Json(new { Past = past, Projected = projected });
    }
}
