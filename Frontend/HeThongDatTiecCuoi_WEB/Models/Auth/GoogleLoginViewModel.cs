using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Auth;

public sealed class GoogleLoginViewModel
{
    [Required]
    public string Credential { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
