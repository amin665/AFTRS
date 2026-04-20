using System.ComponentModel.DataAnnotations;

namespace AFTRS.Models;

public class ReconciliationBatch
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty; // e.g., "October 2025 Reconciliation"

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsFinalized { get; set; } = false; // If true, it disappears from the working dashboard

    // Navigation property: One batch has many transactions
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}