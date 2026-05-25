using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AFTRS.Models;

public class SecurityLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int LogID { get; set; }

    public int? UserID { get; set; }
    public User? User { get; set; }

    [Required]
    [MaxLength(45)]
    public string IPAddress { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty; // Login / Upload

    [Required]
    public bool IsSuccess { get; set; }

    [Required]
    public DateTime Timestamp { get; set; } = DateTime.Now;
}
