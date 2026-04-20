using Microsoft.AspNetCore.Mvc;
using AFTRS.Data;
using AFTRS.Models;
using AFTRS.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace AFTRS.Controllers;

[Authorize(Roles = "FinancialManager,Admin")]
public class ResolveController : Controller
{
    private readonly ApplicationDbContext _context;

    public ResolveController(ApplicationDbContext context)
    {
        _context = context;
    }

    // FR-14: Discrepancy Dashboard (Split-Screen)
    public async Task<IActionResult> Index()
{
    // Find the active batch
    var activeBatch = await _context.ReconciliationBatches.FirstOrDefaultAsync(b => !b.IsFinalized);
    
    if (activeBatch == null) return View(new ResolutionViewModel());

    var model = new ResolutionViewModel
    {
        UnmatchedLedger = await _context.Transactions
            .Where(t => t.BatchId == activeBatch.Id && t.Source == "Ledger" && t.Status == "Unmatched").ToListAsync(),
        UnmatchedBank = await _context.Transactions
            .Where(t => t.BatchId == activeBatch.Id && t.Source == "Bank" && t.Status == "Unmatched").ToListAsync()
    };
    return View(model);
}

    [HttpPost]
public async Task<IActionResult> ForceMatch(int ledgerId, int bankId, string comment)
{
    if (string.IsNullOrEmpty(comment)) return BadRequest("Justification is mandatory.");

    // 1. Find the active batch to get its name
    var activeBatch = await _context.ReconciliationBatches.FirstOrDefaultAsync(b => !b.IsFinalized);
    if (activeBatch == null) return BadRequest("No active session found.");

    var ledgerItem = await _context.Transactions.FindAsync(ledgerId);
    var bankItem = await _context.Transactions.FindAsync(bankId);

    if (ledgerItem != null && bankItem != null)
    {
        ledgerItem.Status = "Resolved";
        bankItem.Status = "Resolved";
        ledgerItem.MatchedTransactionId = bankItem.Id;
        bankItem.MatchedTransactionId = ledgerItem.Id;

        // 2. Log it with the Batch Name
        var audit = new FinancialAuditLog
        {
            LedgerTransactionId = ledgerId,
            BankTransactionId = bankId,
            UserEmail = User.Identity?.Name ?? "Unknown",
            Justification = comment,
            BatchName = activeBatch.Name, // <--- RECORD THE NAME HERE
            Timestamp = DateTime.Now
        };

        _context.FinancialAuditLogs.Add(audit);
        await _context.SaveChangesAsync();
    }

    return RedirectToAction("Index");
}
}