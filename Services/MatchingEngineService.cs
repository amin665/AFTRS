using AFTRS.Data;
using AFTRS.Models;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Services;

public class MatchingEngineService
{
    private readonly ApplicationDbContext _context;
    private readonly decimal _tolerance;

    public MatchingEngineService(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _tolerance = Math.Abs(config.GetValue<decimal>("AppSettings:ToleranceThreshold"));
    }

    // Levenshtein distance-based similarity ratio.
    private static decimal SimilarityRatio(string a, string b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return 0m;
        a = a.Trim().ToLowerInvariant();
        b = b.Trim().ToLowerInvariant();

        int n = a.Length;
        int m = b.Length;
        var d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        var distance = d[n, m];
        var maxLen = Math.Max(n, m);
        if (maxLen == 0) return 1m;
        return 1m - ((decimal)distance / maxLen);
    }

    public async Task<int> RunReconciliationAsync()
    {
        // Memory constraint from SRS: process in batches of 1,000 ledger rows.
        const int batchSize = 1000;

        // Keep bank discrepancies in-memory so we can mark and remove matches deterministically.
        var bank = await _context.Transactions
            .Where(t => t.Source == "Bank" && t.Status == "Discrepancy")
            .ToListAsync();

        int matched = 0;

        while (true)
        {
            var ledgerBatch = await _context.Transactions
                .Where(t => t.Source == "Ledger" && t.Status == "Discrepancy")
                .OrderBy(t => t.TransactionID)
                .Take(batchSize)
                .ToListAsync();

            if (ledgerBatch.Count == 0) break;

            foreach (var l in ledgerBatch)
            {
                // Stage 1: Exact match Date + Amount (+/- tolerance) + Ref.
                var exact = bank.FirstOrDefault(b =>
                    b.Status == "Discrepancy" &&
                    b.TransactionDate.Date == l.TransactionDate.Date &&
                    decimal.Abs(b.Amount - l.Amount) <= _tolerance &&
                    string.Equals(b.ReferenceNumber, l.ReferenceNumber, StringComparison.OrdinalIgnoreCase));

                if (exact != null)
                {
                    LinkPair(l, exact);
                    bank.Remove(exact);
                    matched++;
                    continue;
                }

                // Stage 2: Fuzzy match Amount (+/- tolerance) + description similarity > 80%.
                var fuzzy = bank.FirstOrDefault(b =>
                    b.Status == "Discrepancy" &&
                    decimal.Abs(b.Amount - l.Amount) <= _tolerance &&
                    SimilarityRatio(l.Description, b.Description) > 0.80m);

                if (fuzzy != null)
                {
                    LinkPair(l, fuzzy);
                    bank.Remove(fuzzy);
                    matched++;
                }
            }

            await _context.SaveChangesAsync();
        }

        return matched;
    }

    private static void LinkPair(Transaction ledger, Transaction bank)
    {
        ledger.Status = "Reconciled";
        bank.Status = "Reconciled";
        ledger.MatchMethod = "Auto";
        bank.MatchMethod = "Auto";
        ledger.MatchedTransactionID = bank.TransactionID;
        bank.MatchedTransactionID = ledger.TransactionID;
    }
}
