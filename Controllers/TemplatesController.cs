using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using AFTRS.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
[PermissionAuthorize(AppPermissions.Templates)]
public class TemplatesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ReconciliationSessionContext _sessions;

    public TemplatesController(ApplicationDbContext context, ReconciliationSessionContext sessions)
    {
        _context = context;
        _sessions = sessions;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        var templates = await _context.Templates.Include(t => t.Category).OrderBy(t => t.DescriptionName).ToListAsync();
        return View(templates);
    }

    [HttpGet]
    public async Task<IActionResult> CreateFromTransaction(int transactionId)
    {
        var session = await _sessions.GetSelectedAsync();
        var tx = await _context.Transactions.Include(t => t.Category).FirstOrDefaultAsync(t => t.SessionID == session.SessionID && t.TransactionID == transactionId);
        if (tx == null) return NotFound();

        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();

        var model = new Template
        {
            CategoryID = tx.CategoryID ?? 0,
            DescriptionName = tx.Description,
            Amount = tx.Amount,
            Frequency = "Monthly",
            NextRunDate = DateTime.Today.AddMonths(1)
        };

        return View("Create", model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(new Template { Frequency = "Monthly", NextRunDate = DateTime.Today.AddMonths(1) });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Template model)
    {
        var session = await _sessions.GetSelectedAsync();
        if (session.Status != "Active")
        {
            TempData["Msg"] = UiText.T(Request, "ArchivedSessionReadOnly");
            return RedirectToAction(nameof(Index));
        }

        if (model.CategoryID <= 0)
            ModelState.AddModelError("CategoryID", UiText.T(Request, "CategoryRequired"));

        if (string.IsNullOrWhiteSpace(model.DescriptionName))
            ModelState.AddModelError("DescriptionName", UiText.T(Request, "DescriptionNameRequired"));

        if (model.Amount <= 0)
            ModelState.AddModelError("Amount", UiText.T(Request, "AmountGreaterThanZero"));

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        model.Amount = decimal.Round(model.Amount, 2, MidpointRounding.AwayFromZero);
        _context.Templates.Add(model);
        await _context.SaveChangesAsync();

        TempData["Msg"] = UiText.T(Request, "TemplateCreated");
        return RedirectToAction(nameof(Index));
    }
}
