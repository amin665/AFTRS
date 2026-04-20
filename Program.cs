using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using AFTRS.Data;
using AFTRS.Services;
using QuestPDF.Infrastructure;

// 1. SET QUESTPDF LICENSE (Must be first)
QuestPDF.Settings.License = LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

// 2. DATABASE CONNECTION
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 3. IDENTITY SETUP (Roles enabled)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => {
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Lockout.MaxFailedAccessAttempts = 5;        // FR-03
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5); // FR-03: lockout duration
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 4. COOKIE SETTINGS (Custom Login Path + 30-minute idle timeout FR-05)
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // FR-05: auto session timeout
});

// 5. REGISTER CUSTOM SERVICES
builder.Services.AddScoped<ImportService>();
builder.Services.AddScoped<ReconciliationService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<HeuristicsService>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 6. MIDDLEWARE PIPELINE
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // Must be before Authorization
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 7. SEED DATABASE (Roles & Admin User)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await AFTRS.Utilities.DbSeeder.SeedRolesAndAdminAsync(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.Run();