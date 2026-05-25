using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
public class ResolveController : Controller
{
    private readonly ApplicationDbContext _context;

    public ResolveController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new ResolutionViewModel
        {
            UnmatchedLedger = await _context.Transactions
                .Where(t => t.Source == "Ledger" && t.Status == "Unmatched")
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync(),
            UnmatchedBank = await _context.Transactions
                .Where(t => t.Source == "Bank" && t.Status == "Unmatched")
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForceMatch(int ledgerId, int bankId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            return BadRequest("Justification is mandatory.");

        var ledger = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionID == ledgerId && t.Source == "Ledger");
        var bank = await _context.Transactions.FirstOrDefaultAsync(t => t.TransactionID == bankId && t.Source == "Bank");

        if (ledger == null || bank == null) return NotFound();
        if (ledger.Status != "Unmatched" || bank.Status != "Unmatched") return BadRequest("Both transactions must be Unmatched.");

        var oldLedgerStatus = ledger.Status;
        var oldBankStatus = bank.Status;

        ledger.Status = "Reconciled";
        bank.Status = "Reconciled";
        ledger.MatchedTransactionID = bank.TransactionID;
        bank.MatchedTransactionID = ledger.TransactionID;

        var uid = User.FindFirst(AuthConstants.UserIdClaimType)?.Value;
        if (uid == null) return Forbid();
        var userId = int.Parse(uid);

        // Append-only audit: log for both transactions so trail is complete.
        _context.FinancialAuditLogs.Add(new FinancialAuditLog
        {
            UserID = userId,
            TransactionID = ledger.TransactionID,
            OldStatus = oldLedgerStatus,
            NewStatus = ledger.Status,
            Justification = comment,
            Timestamp = DateTime.Now
        });
        _context.FinancialAuditLogs.Add(new FinancialAuditLog
        {
            UserID = userId,
            TransactionID = bank.TransactionID,
            OldStatus = oldBankStatus,
            NewStatus = bank.Status,
            Justification = comment,
            Timestamp = DateTime.Now
        });

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
