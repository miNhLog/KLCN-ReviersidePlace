namespace HeThongDatTiecCuoi_API.Services;

public interface IGoogleTokenValidator
{
    Task<GoogleUserInfo?> ValidateAsync(string credential, CancellationToken cancellationToken);
}

public sealed record GoogleUserInfo(
    string Subject,
    string Email,
    string DisplayName);
