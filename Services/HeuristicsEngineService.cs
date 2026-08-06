using AFTRS.Data;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Services;

public class HeuristicsEngineService
{
    private readonly ApplicationDbContext _context;
    private readonly decimal _tolerance;

    public HeuristicsEngineService(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _tolerance = Math.Abs(config.GetValue<decimal>("AppSettings:ToleranceThreshold"));
    }

    public async Task<int> ApplyKeywordCategoriesAsync(int sessionId)
    {
        // Uses Category.KeywordRule as the dictionary.
        var categories = await _context.Categories
            .Where(c => c.KeywordRule != null && c.KeywordRule != "")
            .ToListAsync();

        var txs = await _context.Transactions
            .Where(t => t.SessionID == sessionId && t.CategoryID == null)
            .ToListAsync();

        int updated = 0;

        // First, apply templates (UC-09) then keyword rules (UC-10).
        var templates = await _context.Templates.Include(t => t.Category).ToListAsync();

        foreach (var t in txs)
        {
            if (templates.Count > 0)
            {
                var tpl = templates.FirstOrDefault(tp =>
                    !string.IsNullOrWhiteSpace(tp.DescriptionName) &&
                    t.Description.Contains(tp.DescriptionName, StringComparison.OrdinalIgnoreCase) &&
                    decimal.Abs(t.Amount - tp.Amount) <= _tolerance);
                if (tpl != null)
                {
                    t.CategoryID = tpl.CategoryID;
                    updated++;
                    continue;
                }
            }

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
