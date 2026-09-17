namespace HeThongDatTiecCuoi_WEB.Models.Auth;

public sealed record CurrentUserDto(
    int UserId,
    string Email,
    string FullName,
    string? PhoneNumber,
    string RoleName,
    string Status);

public sealed record AuthResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    CurrentUserDto User);

public sealed record ApiErrorDto(string Message, Dictionary<string, string[]>? Errors = null);
public sealed record MessageResponseDto(string Message);

public sealed record ApiCallResult<T>(bool Succeeded, T? Value, string? Error)
{
    public static ApiCallResult<T> Success(T value) => new(true, value, null);
    public static ApiCallResult<T> Failure(string error) => new(false, default, error);
}
