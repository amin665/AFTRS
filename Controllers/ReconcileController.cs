using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
public class ReconcileController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly MatchingEngineService _matching;
    private readonly HeuristicsEngineService _heuristics;

    public ReconcileController(ApplicationDbContext context, MatchingEngineService matching, HeuristicsEngineService heuristics)
    {
        _context = context;
        _matching = matching;
        _heuristics = heuristics;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var transactions = await _context.Transactions
            .Include(t => t.Category)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        return View(transactions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunEngine()
    {
        var matched = await _matching.RunReconciliationAsync();
        var categorized = await _heuristics.ApplyKeywordCategoriesAsync();

        TempData["Msg"] = $"Engine complete: {matched} transaction pairs reconciled, {categorized} categorized.";
        return RedirectToAction(nameof(Index));
    }
}
