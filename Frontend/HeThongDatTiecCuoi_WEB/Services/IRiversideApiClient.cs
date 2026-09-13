using HeThongDatTiecCuoi_WEB.Models.Auth;

namespace HeThongDatTiecCuoi_WEB.Services;

public interface IRiversideApiClient
{
    Task<ApiCallResult<AuthResponseDto>> LoginAsync(LoginViewModel model, CancellationToken cancellationToken);
    Task<ApiCallResult<AuthResponseDto>> RegisterAsync(RegisterViewModel model, CancellationToken cancellationToken);
    Task<ApiCallResult<CurrentUserDto>> GetCurrentUserAsync(string accessToken, CancellationToken cancellationToken);
}
