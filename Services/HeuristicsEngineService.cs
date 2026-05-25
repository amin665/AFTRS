using AFTRS.Data;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Services;

public class HeuristicsEngineService
{
    private readonly ApplicationDbContext _context;

    public HeuristicsEngineService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<int> ApplyKeywordCategoriesAsync()
    {
        // Uses Category.KeywordRule as the dictionary.
        var categories = await _context.Categories
            .Where(c => c.KeywordRule != null && c.KeywordRule != "")
            .ToListAsync();

        if (categories.Count == 0) return 0;

        var txs = await _context.Transactions
            .Where(t => t.CategoryID == null)
            .ToListAsync();

        int updated = 0;

        foreach (var t in txs)
        {
            foreach (var c in categories)
            {
                if (t.Description.Contains(c.KeywordRule!, StringComparison.OrdinalIgnoreCase))
                {
                    t.CategoryID = c.CategoryID;
                    updated++;
                    break;
                }
            }
        }

        if (updated > 0)
            await _context.SaveChangesAsync();

        return updated;
    }
}
