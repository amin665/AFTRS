using AFTRS.Data;
using AFTRS.Infrastructure;
using AFTRS.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AFTRS.Controllers;

[RoleAuthorize("Manager", "Admin")]
public class TemplatesController : Controller
{
    private readonly ApplicationDbContext _context;

    public TemplatesController(ApplicationDbContext context)
    {
        _context = context;
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
        var tx = await _context.Transactions.Include(t => t.Category).FirstOrDefaultAsync(t => t.TransactionID == transactionId);
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
        if (model.CategoryID <= 0)
            ModelState.AddModelError("CategoryID", "Category is required.");

        if (string.IsNullOrWhiteSpace(model.DescriptionName))
            ModelState.AddModelError("DescriptionName", "DescriptionName is required.");

        if (model.Amount <= 0)
            ModelState.AddModelError("Amount", "Amount must be greater than zero.");

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        model.Amount = decimal.Round(model.Amount, 2, MidpointRounding.AwayFromZero);
        _context.Templates.Add(model);
        await _context.SaveChangesAsync();

        TempData["Msg"] = "Template created.";
        return RedirectToAction(nameof(Index));
    }
}
