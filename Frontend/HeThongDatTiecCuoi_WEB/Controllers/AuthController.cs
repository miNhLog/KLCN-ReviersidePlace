using System.Security.Claims;
using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class AuthController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";
    private readonly IRiversideApiClient _apiClient;

    public AuthController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet("dang-nhap")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        var hasApiToken = !string.IsNullOrWhiteSpace(Request.Cookies[ApiTokenCookie]);

        // CHỈ chuyển hướng vào trong khi CẢ HAI cookie rp_auth VÀ rp_api_token đều còn hợp lệ
        if (User.Identity?.IsAuthenticated == true && hasApiToken)
        {
            var accessToken = Request.Cookies[ApiTokenCookie];

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                await HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);

                Response.Cookies.Delete(ApiTokenCookie);

                return View(new LoginViewModel
                {
                    ReturnUrl = returnUrl
                });
            }

            return RedirectForRole(User.FindFirstValue(ClaimTypes.Role));
        }

        // Nếu có rp_auth nhưng mất rp_api_token (phiên lỗi), tự động dọn dẹp sạch sẽ
        if (User.Identity?.IsAuthenticated == true && !hasApiToken)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            Response.Cookies.Delete(ApiTokenCookie);
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
            return View(model);
        }

        var result = await _apiClient.LoginAsync(model, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Đăng nhập không thành công.");
            return View(model);
        }

        await SignInAsync(result.Value, model.RememberMe);
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return LocalRedirect(model.ReturnUrl);
        }

        return RedirectForRole(result.Value.User.RoleName);
    }

    [HttpGet("dang-ky")]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectForRole(User.FindFirstValue(ClaimTypes.Role));
        }

        return View(new RegisterViewModel());
    }

    [HttpPost("dang-ky")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _apiClient.RegisterAsync(model, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Đăng ký không thành công.");
            return View(model);
        }

        await SignInAsync(result.Value, isPersistent: false);
        TempData["Success"] = "Tạo tài khoản thành công. Chào mừng anh/chị đến Riverside Palace!";
        return RedirectToAction("Index", "Home");
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

    private async Task SignInAsync(AuthResponseDto auth, bool isPersistent)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, auth.User.UserId.ToString()),
            new(ClaimTypes.Name, auth.User.FullName),
            new(ClaimTypes.Email, auth.User.Email),
            new(ClaimTypes.Role, auth.User.RoleName)
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

        var expireTime = auth.ExpiresAtUtc > DateTime.UtcNow
    ? new DateTimeOffset(auth.ExpiresAtUtc)
    : DateTimeOffset.UtcNow.AddHours(1);

        Response.Cookies.Append(ApiTokenCookie, auth.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            Expires = expireTime
        });
    }
}
