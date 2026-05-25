using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class FileUploadRecord
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int FileUploadRecordID { get; set; }

    [Required]
    public string FileName { get; set; } = string.Empty;

    [Required]
    public string FileHash { get; set; } = string.Empty; // MD5/SHA-256 (we use SHA-256)

    [Required]
    public string Source { get; set; } = string.Empty; // Ledger / Bank

    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.Now;
}
