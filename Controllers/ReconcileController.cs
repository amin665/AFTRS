using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
[PermissionAuthorize(AppPermissions.Reconcile)]
public class ReconcileController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly MatchingEngineService _matching;
    private readonly HeuristicsEngineService _heuristics;
    private readonly ReconciliationSessionContext _sessions;

    public ReconcileController(ApplicationDbContext context, MatchingEngineService matching, HeuristicsEngineService heuristics, ReconciliationSessionContext sessions)
    {
        _context = context;
        _matching = matching;
        _heuristics = heuristics;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var session = await _sessions.GetSelectedAsync();
        ViewBag.Session = session;
        var transactions = await _context.Transactions
            .Where(t => t.SessionID == session.SessionID)
            .Include(t => t.Category)
            .Include(t => t.MatchedTransaction)
            .ToListAsync();

        ViewBag.AutoReconciledCount = transactions.Count(t => t.Status == "Reconciled" && t.MatchMethod == "Auto");
        ViewBag.ManualReconciledCount = transactions.Count(t => t.Status == "Reconciled" && t.MatchMethod == "Manual");
        ViewBag.DiscrepancyCount = transactions.Count(t => t.Status == "Discrepancy");
        transactions = transactions
            .OrderBy(t => t.Status == "Reconciled" && t.MatchMethod == "Auto" ? 0 : t.Status == "Reconciled" && t.MatchMethod == "Manual" ? 1 : 2)
            .ThenByDescending(t => t.TransactionDate)
            .ToList();

        return View(transactions);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunEngine()
    {
        var session = await _sessions.GetSelectedAsync();
        if (session.Status != "Active")
        {
            TempData["Msg"] = UiText.T(Request, "ArchivedSessionReadOnly");
            return RedirectToAction(nameof(Index));
        }

        if (session.ReconciledAt != null)
        {
            TempData["Msg"] = UiText.T(Request, "ReconciliationAlreadyDone");
            return RedirectToAction(nameof(Index));
        }

        var matched = await _matching.RunReconciliationAsync(session.SessionID);
        var categorized = await _heuristics.ApplyKeywordCategoriesAsync(session.SessionID);
        session.ReconciledAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["Msg"] = string.Format(UiText.T(Request, "EngineComplete"), matched, categorized);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CategorizeOnly()
    {
        var session = await _sessions.GetSelectedAsync();
        if (session.Status != "Active")
        {
            TempData["Msg"] = UiText.T(Request, "ArchivedSessionReadOnly");
            return RedirectToAction(nameof(Index));
        }

        var categorized = await _heuristics.ApplyKeywordCategoriesAsync(session.SessionID);
        TempData["Msg"] = string.Format(UiText.T(Request, "CategorizationComplete"), categorized);
        return RedirectToAction(nameof(Index));
    }
}
