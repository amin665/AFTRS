using AFTRS.Data;
using AFTRS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AFTRS.Services;

public class ReconciliationService
{
    private readonly ApplicationDbContext _context;
    private readonly decimal _toleranceThreshold;

    public ReconciliationService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        // SRS 2.1.7: Tolerance threshold (default 0.05 LYD)
        _toleranceThreshold = configuration.GetValue<decimal>("AppSettings:ToleranceThreshold", 0.05m);
    }

    // 1. LEVENSHTEIN DISTANCE ALGORITHM (Requirement FR-11)
    private double CalculateSimilarity(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target)) return 0;
        
        source = source.ToLower(); target = target.ToLower();
        int n = source.Length; int m = target.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; d[i, 0] = i++) ;
        for (int j = 0; j <= m; d[0, j] = j++) ;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (target[j - 1] == source[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return 1.0 - ((double)d[n, m] / Math.Max(source.Length, target.Length));
    }

    // 2. THE CORE ENGINE
    public async Task RunReconciliationAsync(int batchId)
    {
        var ledgerItems = await _context.Transactions
        .Where(t => t.BatchId == batchId && t.Source == "Ledger" && t.Status == "Unmatched").ToListAsync();
        
        var bankItems = await _context.Transactions
        .Where(t => t.BatchId == batchId && t.Source == "Bank" && t.Status == "Unmatched").ToListAsync();

        foreach (var ledger in ledgerItems)
        {
            // LEVEL 1: EXACT MATCH (Date, Amount, Reference) — FR-10
            var exactMatch = bankItems.FirstOrDefault(b => 
                b.TransactionDate.Date == ledger.TransactionDate.Date && 
                b.Amount == ledger.Amount && 
                b.ReferenceNumber == ledger.ReferenceNumber &&
                b.Status == "Unmatched");

            if (exactMatch != null)
            {
                MatchTransactions(ledger, exactMatch, "Reconciled");
                continue;
            }

            // LEVEL 2: FUZZY MATCH — FR-11
            // Amount must match within tolerance threshold (SRS 2.1.7), description similarity > 80%
            var fuzzyMatch = bankItems.FirstOrDefault(b => 
                b.Status == "Unmatched" &&
                Math.Abs(b.Amount - ledger.Amount) <= _toleranceThreshold &&
                CalculateSimilarity(ledger.Description, b.Description) >= 0.80);

            if (fuzzyMatch != null)
            {
                MatchTransactions(ledger, fuzzyMatch, "Reconciled");
            }
        }
        await _context.SaveChangesAsync();
    }

    private void MatchTransactions(Transaction t1, Transaction t2, string status)
    {
        t1.Status = status;
        t2.Status = status;
        t1.MatchedTransactionId = t2.Id;
        t2.MatchedTransactionId = t1.Id;
    }
}
