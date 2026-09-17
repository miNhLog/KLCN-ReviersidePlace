using HeThongDatTiecCuoi_API.Constants.StatusCodes;

namespace HeThongDatTiecCuoi_API.Models;

public sealed class User
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Status { get; set; } = AccountStatusCodes.Active;
    public DateTime CreatedAt { get; set; }

    public Role Role { get; set; } = null!;
    public Customer? Customer { get; set; }
    public Employee? Employee { get; set; }
}
