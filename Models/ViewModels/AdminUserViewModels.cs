using System.ComponentModel.DataAnnotations;
using AFTRS.Models;

namespace AFTRS.ViewModels;

public class AdminUsersViewModel
{
    public List<User> Users { get; set; } = new();
}

public class AdminCreateUserViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Compare("Password", ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Manager";

    public List<string> Permissions { get; set; } = new();
}

public class AdminEditUserPermissionsViewModel
{
    public int UserID { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsLocked { get; set; }
    public List<string> Permissions { get; set; } = new();

    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
    public string? ConfirmNewPassword { get; set; }
}
