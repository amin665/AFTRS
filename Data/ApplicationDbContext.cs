using Microsoft.EntityFrameworkCore;
using AFTRS.Models;

namespace AFTRS.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<Transaction> Transactions { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<BudgetTarget> BudgetTargets { get; set; }
    public DbSet<Template> Templates { get; set; }
    public DbSet<FinancialAuditLog> FinancialAuditLogs { get; set; }
    public DbSet<SecurityLog> SecurityLogs { get; set; }
    public DbSet<FileUploadRecord> FileUploadRecords { get; set; }
    public DbSet<ReconciliationSession> ReconciliationSessions { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("Users");
            b.HasKey(x => x.UserID);
            b.Property(x => x.Username).HasMaxLength(50).IsRequired();
            b.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
            b.Property(x => x.Role).HasMaxLength(20).IsRequired();
            b.HasIndex(x => x.Username).IsUnique();
        });

        modelBuilder.Entity<Category>(b =>
        {
            b.ToTable("Categories");
            b.HasKey(x => x.CategoryID);
            b.Property(x => x.Name).HasMaxLength(50).IsRequired();
            b.Property(x => x.KeywordRule).HasMaxLength(100);
        });

        modelBuilder.Entity<Transaction>(b =>
        {
            // SRS FR-09: imported records are stored in Staging_Transactions.
            b.ToTable("Staging_Transactions");
            b.HasKey(x => x.TransactionID);
            b.Property(x => x.Description).HasMaxLength(200).IsRequired();
            b.Property(x => x.ReferenceNumber).HasMaxLength(50);
            b.Property(x => x.Source).HasMaxLength(20).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
            b.Property(x => x.MatchMethod).HasMaxLength(20);
            b.Property(x => x.DiscrepancyComment).HasMaxLength(1000);
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");

            // Self-referencing match relationship.
            b.HasOne(x => x.MatchedTransaction)
                .WithOne()
                .HasForeignKey<Transaction>(x => x.MatchedTransactionID)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryID)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BudgetTarget>(b =>
        {
            b.ToTable("BudgetTargets");
            b.HasKey(x => x.BudgetID);
            b.Property(x => x.TargetAmount).HasColumnType("decimal(18,2)");
            b.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryID)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Template>(b =>
        {
            b.ToTable("Templates");
            b.HasKey(x => x.TemplateID);
            b.Property(x => x.DescriptionName).HasMaxLength(100).IsRequired();
            b.Property(x => x.Amount).HasColumnType("decimal(18,2)");
            b.Property(x => x.Frequency).HasMaxLength(20).IsRequired();
            b.HasOne(x => x.Category)
                .WithMany()
                .HasForeignKey(x => x.CategoryID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FinancialAuditLog>(b =>
        {
            b.ToTable("FinancialAuditLogs");
            b.HasKey(x => x.AuditID);
            b.Property(x => x.OldStatus).HasMaxLength(20).IsRequired();
            b.Property(x => x.NewStatus).HasMaxLength(20).IsRequired();
            b.Property(x => x.Justification).IsRequired();
            b.HasOne(x => x.Transaction)
                .WithMany()
                .HasForeignKey(x => x.TransactionID)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionID)
                .OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SecurityLog>(b =>
        {
            b.ToTable("SecurityLogs");
            b.HasKey(x => x.LogID);
            b.Property(x => x.IPAddress).HasMaxLength(45).IsRequired();
            b.Property(x => x.Action).HasMaxLength(50).IsRequired();
            b.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserID)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FileUploadRecord>(b =>
        {
            b.ToTable("FileUploadRecords");
            b.HasKey(x => x.FileUploadRecordID);
            b.Property(x => x.Source).HasMaxLength(20).IsRequired();
            b.HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionID)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserPermission>(b =>
        {
            b.ToTable("UserPermissions");
            b.HasKey(x => x.UserPermissionID);
            b.Property(x => x.Permission).HasMaxLength(50).IsRequired();
            b.HasIndex(x => new { x.UserID, x.Permission }).IsUnique();
            b.HasOne(x => x.User)
                .WithMany(x => x.Permissions)
                .HasForeignKey(x => x.UserID)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReconciliationSession>(b =>
        {
            b.ToTable("ReconciliationSessions");
            b.HasKey(x => x.SessionID);
            b.Property(x => x.Name).HasMaxLength(100).IsRequired();
            b.Property(x => x.Status).HasMaxLength(20).IsRequired();
        });
    }
}
