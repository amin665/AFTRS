using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
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
        TempData["Msg"] = "Budget target saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(string name, string keywordRule)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Category name is required.";
            return RedirectToAction(nameof(Index));
        }

        var trimmedName = name.Trim();
        var exists = await _context.Categories.AnyAsync(c => c.Name == trimmedName);
        if (exists)
        {
            TempData["Error"] = "Category already exists.";
            return RedirectToAction(nameof(Index));
        }

        _context.Categories.Add(new Category
        {
            Name = trimmedName,
            KeywordRule = string.IsNullOrWhiteSpace(keywordRule) ? null : keywordRule.Trim()
        });

        await _context.SaveChangesAsync();
        TempData["Msg"] = "Category keyword rule saved.";
        return RedirectToAction(nameof(Index));
    }

    // FR-20: Cash Flow projection based on average spending over previous 6 months, project next N months (default 3).
    [HttpGet]
    public async Task<IActionResult> GetCashFlowData(int projectionMonths = 3)
    {
        var today = DateTime.Today;
        if (projectionMonths < 1 || projectionMonths > 12) projectionMonths = 3;

        var start = today.AddMonths(-6);
        var tx = await _context.Transactions
            .Where(t => t.Source == "Ledger" && t.TransactionDate >= start)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync();

        // Monthly totals for past 6 months (including current partial).
        var past = tx
            .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.Amount) })
            .OrderBy(x => x.Year).ThenBy(x => x.Month)
            .ToList();

        decimal avg = past.Count > 0 ? past.Average(x => x.Total) : 0m;
        var projected = new List<object>();
        for (int i = 1; i <= projectionMonths; i++)
        {
            var d = new DateTime(today.Year, today.Month, 1).AddMonths(i);
            projected.Add(new { Year = d.Year, Month = d.Month, Total = decimal.Round(avg, 2) });
        }

        return Json(new { Past = past, Projected = projected });
    }
}
