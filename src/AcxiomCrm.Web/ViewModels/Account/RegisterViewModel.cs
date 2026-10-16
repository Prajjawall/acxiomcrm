using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Web.ViewModels.Account;

public sealed class RegisterViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [MaxLength(150, ErrorMessage = "Full name cannot exceed 150 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required.")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required.")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
