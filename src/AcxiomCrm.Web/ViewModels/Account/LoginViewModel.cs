using System.ComponentModel.DataAnnotations;

namespace AcxiomCrm.Web.ViewModels.Account;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Username or email is required.")]
    public string UserNameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}
