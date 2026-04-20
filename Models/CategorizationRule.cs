using System.ComponentModel.DataAnnotations;

namespace AFTRS.Models;

public class CategorizationRule
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Keyword { get; set; } = string.Empty; // e.g., "LTT"

    [Required]
    public string Category { get; set; } = string.Empty; // e.g., "Utilities"

    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}