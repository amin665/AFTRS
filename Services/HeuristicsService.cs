using AFTRS.Data;
using AFTRS.Models;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Services;

public class HeuristicsService
{
    private readonly ApplicationDbContext _context;

    public HeuristicsService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> RunAutoCategorizationAsync(int batchId)
    {
        // 1. Get all rules
        var rules = await _context.CategorizationRules.ToListAsync();
        if (!rules.Any()) return 0;

        // 2. Get all transactions in this batch that don't have a category yet
        var transactions = await _context.Transactions
            .Where(t => t.BatchId == batchId && (t.Category == null || t.Category == ""))
            .ToListAsync();

        int updatedCount = 0;

        foreach (var t in transactions)
        {
            foreach (var rule in rules)
            {
                // Use IndexOf with StringComparison.OrdinalIgnoreCase for better matching
                if (t.Description.Contains(rule.Keyword, StringComparison.OrdinalIgnoreCase))
                {
                    t.Category = rule.Category;
                    updatedCount++;
                    break; // Move to next transaction once a rule matches
                }
            }
        }

        if (updatedCount > 0)
        {
            await _context.SaveChangesAsync();
        }

        return updatedCount;
    }
}