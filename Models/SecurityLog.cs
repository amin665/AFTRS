using System.ComponentModel.DataAnnotations;

namespace AFTRS.Models;

public class SecurityLog
{
    [Key]
    public int Id { get; set; }
    
    public string? UserId { get; set; } // Tracks who tried to login
    
    [Required]
    public DateTime Timestamp { get; set; } = DateTime.Now;
    
    [Required]
    public string IPAddress { get; set; } = string.Empty;
    
    [Required]
    public string Event { get; set; } = string.Empty; // e.g., "Login Attempt"
    
    [Required]
    public string Status { get; set; } = string.Empty; // Success or Failure (FR-04)
}