using Google.Apis.Auth;
using HeThongDatTiecCuoi_API.Options;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly GoogleAuthOptions _options;

    public GoogleTokenValidator(IOptions<GoogleAuthOptions> options)
    {
        _options = options.Value;
    }

    public async Task<GoogleUserInfo?> ValidateAsync(
        string credential,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException(
                "Google Login chưa được cấu hình. Hãy thiết lập GoogleAuth:ClientId.");
        }

        if (string.IsNullOrWhiteSpace(credential))
        {
            return null;
        }

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [_options.ClientId]
            };

            var payload = await GoogleJsonWebSignature
                .ValidateAsync(credential, settings)
                .WaitAsync(cancellationToken);

            if (!payload.EmailVerified ||
                string.IsNullOrWhiteSpace(payload.Subject) ||
                string.IsNullOrWhiteSpace(payload.Email))
            {
                return null;
            }

            var email = payload.Email.Trim().ToLowerInvariant();
            var displayName = string.IsNullOrWhiteSpace(payload.Name)
                ? email.Split('@')[0]
                : payload.Name.Trim();

            return new GoogleUserInfo(payload.Subject, email, displayName);
        }
        catch (InvalidJwtException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
