using System.Security.Claims;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Options;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class AuthController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;
    private readonly GoogleAuthOptions _googleAuthOptions;

    public AuthController(
        IRiversideApiClient apiClient,
        IOptions<GoogleAuthOptions> googleAuthOptions)
    {
        _apiClient = apiClient;
        _googleAuthOptions = googleAuthOptions.Value;
    }

    [HttpGet("dang-nhap")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
<<<<<<< Updated upstream
        if (User.Identity?.IsAuthenticated == true)
=======
        SetGoogleClientId();
        var hasApiToken = !string.IsNullOrWhiteSpace(Request.Cookies[ApiTokenCookie]);

        // CHỈ chuyển hướng vào trong khi CẢ HAI cookie rp_auth VÀ rp_api_token đều còn hợp lệ
        if (User.Identity?.IsAuthenticated == true && hasApiToken)
>>>>>>> Stashed changes
        {
            return RedirectToAction("Dashboard", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("dang-nhap")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SetGoogleClientId();
            return View(model);
        }

        var result = await _apiClient.LoginAsync(model, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Đăng nhập không thành công.");
            SetGoogleClientId();
            return View(model);
        }

        await SignInAsync(result.Value, model.GhiNhoDangNhap);
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectToAction("Dashboard", "Home");
    }

    [HttpGet("dang-ky")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard", "Home");
        }

        SetGoogleClientId();
        return View(new RegisterViewModel());
    }

    [HttpPost("dang-ky")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            SetGoogleClientId();
            return View(model);
        }

        var result = await _apiClient.RegisterAsync(model, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Đăng ký không thành công.");
            SetGoogleClientId();
            return View(model);
        }

        await SignInAsync(result.Value, isPersistent: false);
        TempData["Success"] = "Tạo tài khoản thành công. Chào mừng anh/chị đến Riverside Palace!";
<<<<<<< Updated upstream
        return RedirectToAction("Dashboard", "Home");
=======
        return RedirectToAction("Index", "Home");
    }

    [HttpPost("auth/google")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Google(
        GoogleLoginViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Không nhận được thông tin xác thực từ Google.";
            return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
        }

        var result = await _apiClient.GoogleLoginAsync(model, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            TempData["Error"] = result.Error ?? "Đăng nhập Google không thành công.";
            return RedirectToAction(nameof(Login), new { returnUrl = model.ReturnUrl });
        }

        await SignInAsync(result.Value, model.RememberMe);
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectForRole(result.Value.User.RoleName);
    }

    [HttpGet("auth/forgot-password")]
    [AllowAnonymous]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [HttpPost("auth/forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.ForgotPasswordAsync(model, cancellationToken);
        TempData[result.Succeeded ? "Success" : "Error"] = result.Succeeded
            ? result.Value?.Message ?? "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi."
            : result.Error ?? "Không thể gửi yêu cầu đặt lại mật khẩu.";
        return View(model);
    }

    [HttpGet("auth/reset-password")]
    [AllowAnonymous]
    public IActionResult ResetPassword(string? token)
    {
        return View(new ResetPasswordViewModel { Token = token ?? string.Empty });
    }

    [HttpPost("auth/reset-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.ResetPasswordAsync(model, cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Không thể đặt lại mật khẩu.");
            return View(model);
        }

        TempData["Success"] = result.Value?.Message ?? "Đặt lại mật khẩu thành công.";
        return RedirectToAction(nameof(Login));
>>>>>>> Stashed changes
    }

    [HttpPost("dang-xuat")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        Response.Cookies.Delete(ApiTokenCookie);
        return RedirectToAction(nameof(Login));
    }

<<<<<<< Updated upstream
=======
    private IActionResult RedirectForRole(string? roleName)
    {
        if (roleName == RoleNames.Admin)
        {
            return RedirectToAction("Index", "AdminHall");
        }

        if (roleName is RoleNames.Consultant or RoleNames.Coordinator)
        {
            return RedirectToAction("Dashboard", "Home");
        }

        return RedirectToAction("Index", "Home");
    }

    private void SetGoogleClientId()
    {
        ViewData["GoogleClientId"] = _googleAuthOptions.ClientId;
    }

>>>>>>> Stashed changes
    private async Task SignInAsync(AuthResponseDto auth, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.NguoiDungId.ToString()),
            new(ClaimTypes.Name, auth.User.HoTen),
            new(ClaimTypes.Email, auth.User.Email),
            new(ClaimTypes.Role, auth.User.VaiTro)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = new DateTimeOffset(auth.ExpiresAtUtc)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            properties);

        Response.Cookies.Append(ApiTokenCookie, auth.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = new DateTimeOffset(auth.ExpiresAtUtc)
        });
    }
}
