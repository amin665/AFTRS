using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AFTRS.Models;

namespace AFTRS.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // This will store our Security Logs (FR-04)
    public DbSet<SecurityLog> SecurityLogs { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<FinancialAuditLog> FinancialAuditLogs { get; set; }
    public DbSet<Budget> Budgets { get; set; }
    public DbSet<ReconciliationBatch> ReconciliationBatches { get; set; } 
    public DbSet<CategorizationRule> CategorizationRules { get; set; }
    public DbSet<FileUploadRecord> FileUploadRecords { get; set; }
}