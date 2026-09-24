using System.ComponentModel.DataAnnotations;

namespace HeThongDatTiecCuoi_API.DTOs.Auth;

public sealed class GoogleLoginRequest
{
    [Required(ErrorMessage = "Thiếu thông tin xác thực Google.")]
    public string Credential { get; init; } = string.Empty;

    public bool RememberMe { get; init; }
}
