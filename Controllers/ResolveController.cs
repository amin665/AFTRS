using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using AFTRS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
[PermissionAuthorize(AppPermissions.ResolveDiscrepancies)]
public class ResolveController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReconciliationSessionContext _sessions;

    public ResolveController(ApplicationDbContext context, ReconciliationSessionContext sessions)
    {
        _context = context;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var session = await _sessions.GetSelectedAsync();
        ViewBag.Session = session;
        var model = new ResolutionViewModel
        {
            LedgerDiscrepancies = await _context.Transactions
                .Where(t => t.SessionID == session.SessionID && t.Source == "Ledger" && t.Status == "Discrepancy")
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync(),
            BankDiscrepancies = await _context.Transactions
                .Where(t => t.SessionID == session.SessionID && t.Source == "Bank" && t.Status == "Discrepancy")
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForceMatch(int ledgerId, int bankId, string comment)
    {
        var session = await _sessions.GetSelectedAsync();
        if (session.Status != "Active") return BadRequest(UiText.T(Request, "ArchivedSessionReadOnly"));
        if (string.IsNullOrWhiteSpace(comment))
            return BadRequest(UiText.T(Request, "JustificationMandatory"));

        var ledger = await _context.Transactions.FirstOrDefaultAsync(t => t.SessionID == session.SessionID && t.TransactionID == ledgerId && t.Source == "Ledger");
        var bank = await _context.Transactions.FirstOrDefaultAsync(t => t.SessionID == session.SessionID && t.TransactionID == bankId && t.Source == "Bank");

        if (ledger == null || bank == null) return NotFound();
        if (ledger.Status != "Discrepancy" || bank.Status != "Discrepancy") return BadRequest(UiText.T(Request, "BothDiscrepancies"));

        var oldLedgerStatus = ledger.Status;

        ledger.Status = "Reconciled";
        bank.Status = "Reconciled";
        ledger.MatchMethod = "Manual";
        bank.MatchMethod = "Manual";
        ledger.MatchedTransactionID = bank.TransactionID;
        bank.MatchedTransactionID = ledger.TransactionID;

        var uid = User.FindFirst(AuthConstants.UserIdClaimType)?.Value;
        if (uid == null) return Forbid();
        var userId = int.Parse(uid);

        // A manual link is one business action, so record one append-only audit event for the pair.
        _context.FinancialAuditLogs.Add(new FinancialAuditLog
        {
            SessionID = session.SessionID,
            UserID = userId,
            TransactionID = ledger.TransactionID,
            OldStatus = oldLedgerStatus,
            NewStatus = ledger.Status,
            Justification = $"Matched Ledger #{ledger.TransactionID} with Bank #{bank.TransactionID}: {comment.Trim()}",
            Timestamp = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDiscrepancyComment(int transactionId, string? comment)
    {
        var session = await _sessions.GetSelectedAsync();
        if (session.Status != "Active") return BadRequest(UiText.T(Request, "ArchivedSessionReadOnly"));

        var transaction = await _context.Transactions.FirstOrDefaultAsync(t =>
            t.SessionID == session.SessionID &&
            t.TransactionID == transactionId &&
            t.Status == "Discrepancy");

        if (transaction == null) return NotFound();

        transaction.DiscrepancyComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        await _context.SaveChangesAsync();

        TempData["Msg"] = UiText.T(Request, "DiscrepancyCommentSaved");
        return RedirectToAction(nameof(Index));
    }
}
