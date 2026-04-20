using Microsoft.AspNetCore.Mvc;
using AFTRS.Data;
using AFTRS.Models;
using Microsoft.EntityFrameworkCore;
using AFTRS.Services;
namespace AFTRS.Controllers;

public class StrategicController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReportService _reportService;
    private readonly HeuristicsService _heuristicsService;

    public StrategicController(ApplicationDbContext context, ReportService reportService, HeuristicsService heuristicsService)
    {
        _context = context;
        _reportService = reportService;
        _heuristicsService = heuristicsService;
    }

    // FR-18: View Budgets
    public async Task<IActionResult> Index()
{
    var budgets = await _context.Budgets.ToListAsync();
    ViewBag.Rules = await _context.CategorizationRules.ToListAsync();

    var actualSpending = await _context.Transactions
        .Where(t => !string.IsNullOrEmpty(t.Category) && t.Source == "Ledger") 
        .GroupBy(t => t.Category)
        .Select(g => new { 
            Category = g.Key, 
            Total = g.Sum(x => x.Amount) 
        })
        .ToDictionaryAsync(x => x.Category!, x => x.Total);

    ViewBag.ActualSpending = actualSpending;
    return View(budgets);
}

    [HttpPost]
    public async Task<IActionResult> SetBudget(string category, decimal limit)
    {
        var budget = new Budget { CategoryName = category, MonthlyLimit = limit };
        _context.Budgets.Add(budget);
        await _context.SaveChangesAsync();
        return RedirectToAction("Index");
    }

    // FR-20: Cash Flow Projection Data – historical + 3-month forward projection
    public async Task<IActionResult> GetChartData()
    {
        var transactions = await _context.Transactions
            .Where(t => (t.Status == "Reconciled" || t.Status == "Resolved") && t.Source == "Ledger")
            .ToListAsync();

        // Historical daily totals
        var historical = transactions
            .GroupBy(t => t.TransactionDate.Date)
            .Select(g => new { Date = g.Key, Total = g.Sum(x => x.Amount) })
            .OrderBy(x => x.Date)
            .ToList();

        // FR-20: Project next 3 months based on average spending of previous 6 months
        var sixMonthsAgo = DateTime.Today.AddMonths(-6);
        var recentTransactions = transactions
            .Where(t => t.TransactionDate >= sixMonthsAgo)
            .ToList();

        decimal avgMonthlySpending = 0;
        if (recentTransactions.Any())
        {
            avgMonthlySpending = recentTransactions
                .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                .Select(g => g.Sum(x => x.Amount))
                .DefaultIfEmpty(0)
                .Average();
        }

        // Build projected data points (monthly average distributed over ~30 days)
        var projectedPoints = new List<object>();
        var today = DateTime.Today;
        for (int m = 1; m <= 3; m++)
        {
            var projDate = today.AddMonths(m);
            projectedPoints.Add(new
            {
                Date = projDate.ToString("yyyy-MM-dd"),
                Total = Math.Round(avgMonthlySpending, 2),
                IsProjection = true
            });
        }

        return Json(new
        {
            Historical = historical.Select(h => new { Date = h.Date.ToString("yyyy-MM-dd"), Total = h.Total }),
            Projected = projectedPoints
        });
    }

public async Task<IActionResult> Archive(string searchTerm, DateTime? startDate, DateTime? endDate)
{
    var query = _context.ReconciliationBatches
        .Include(b => b.Transactions)
        .Where(b => b.IsFinalized); // Only show finished work

    if (!string.IsNullOrEmpty(searchTerm))
    {
        query = query.Where(b => b.Name.Contains(searchTerm));
    }

    if (startDate.HasValue)
    {
        query = query.Where(b => b.CreatedAt >= startDate.Value);
    }

    if (endDate.HasValue)
    {
        query = query.Where(b => b.CreatedAt <= endDate.Value);
    }

    return View(await query.OrderByDescending(b => b.CreatedAt).ToListAsync());
}

// GET: Strategic/BatchDetails/5
public async Task<IActionResult> BatchDetails(int id)
{
    var batch = await _context.ReconciliationBatches
        .Include(b => b.Transactions)
        .FirstOrDefaultAsync(b => b.Id == id);

    if (batch == null) return NotFound();

    return View(batch);
}
public async Task<IActionResult> DownloadReport(int id)
{
    var batch = await _context.ReconciliationBatches
        .Include(b => b.Transactions)
        .FirstOrDefaultAsync(b => b.Id == id);

    if (batch == null) return NotFound();

    var pdfBytes = _reportService.GenerateBatchPdf(batch);
    return File(pdfBytes, "application/pdf", $"{batch.Name}_Report.pdf");
}

[HttpPost]
public async Task<IActionResult> CreateRule(string keyword, string category)
{
    var rule = new CategorizationRule { Keyword = keyword, Category = category };
    _context.CategorizationRules.Add(rule);
    await _context.SaveChangesAsync();
    return RedirectToAction("Index");
}
[HttpPost]
[HttpPost]
public async Task<IActionResult> AutoCategorize()
{
    // Find the currently active (unfinalized) session
    var activeBatch = await _context.ReconciliationBatches
        .FirstOrDefaultAsync(b => !b.IsFinalized);

    if (activeBatch == null)
    {
        TempData["Error"] = "No active session found to categorize.";
        return RedirectToAction("Index");
    }

    // Run the service
    int updated = await _heuristicsService.RunAutoCategorizationAsync(activeBatch.Id);

    if (updated > 0)
    {
        TempData["Msg"] = $"Success! The Heuristics Engine identified and categorized {updated} transactions.";
    }
    else
    {
        TempData["Msg"] = "Process complete. No new matches found for your current rules.";
    }

    return RedirectToAction("Index");
}
}