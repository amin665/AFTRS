using System.ComponentModel.DataAnnotations;

namespace AFTRS.Models;

/// <summary>
/// Tracks uploaded file names and SHA-256 hashes to prevent duplicate uploads (FR-09a).
/// </summary>
public class FileUploadRecord
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FileHash { get; set; } = string.Empty; // SHA-256 hex digest

    [Required]
    public string Source { get; set; } = string.Empty; // "Ledger" or "Bank"

    public string? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.Now;

    public int BatchId { get; set; }
}
