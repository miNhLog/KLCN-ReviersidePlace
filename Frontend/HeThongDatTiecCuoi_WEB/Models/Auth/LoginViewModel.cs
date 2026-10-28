using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_WEB.Models.Auth;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập email, số điện thoại hoặc mã nhân viên.")]
    [Display(Name = "Email, số điện thoại hoặc mã nhân viên")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
}
