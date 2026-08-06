using AFTRS.Data;
using AFTRS.Models;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Services;

public class ReconciliationSessionContext
{
    private const string CookieName = "AFTRS.SessionId";
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReconciliationSessionContext(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ReconciliationSession> GetSelectedAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (int.TryParse(httpContext?.Request.Cookies[CookieName], out var selectedId))
        {
            var selected = await _context.ReconciliationSessions.FindAsync(selectedId);
            if (selected != null) return selected;
        }

        var active = await _context.ReconciliationSessions
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(s => s.Status == "Active");

        if (active == null)
        {
            active = new ReconciliationSession { Name = "Initial Session" };
            _context.ReconciliationSessions.Add(active);
            await _context.SaveChangesAsync();
        }

        Select(active.SessionID);
        return active;
    }

    public void Select(int sessionId)
    {
        _httpContextAccessor.HttpContext?.Response.Cookies.Append(CookieName, sessionId.ToString(), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddYears(1)
        });
    }
}
