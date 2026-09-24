namespace HeThongDatTiecCuoi_API.Models;

public sealed class ExternalLogin
{
    public long ExternalLoginId { get; set; }
    public int UserId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ProviderUserId { get; set; } = string.Empty;
    public string? ProviderEmail { get; set; }
    public DateTime LinkedAt { get; set; }

    public User User { get; set; } = null!;
}
