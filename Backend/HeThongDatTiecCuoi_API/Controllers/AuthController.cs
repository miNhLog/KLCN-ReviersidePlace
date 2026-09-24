using System.Security.Claims;
using HeThongDatTiecCuoi_API.DTOs.Auth;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RegisterAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(request, cancellationToken);
        return ToActionResult(result);
    }

<<<<<<< Updated upstream
=======
    [HttpPost("google")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Google(
        GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.GoogleLoginAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _passwordResetService.SendResetLinkForEmailAsync(
                request.Email, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Không thể gửi email đặt lại mật khẩu.");
        }

        return Ok(new
        {
            message = "Nếu email tồn tại trong hệ thống, hướng dẫn đặt lại mật khẩu sẽ được gửi."
        });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _passwordResetService.ResetPasswordAsync(
            request.Token,
            request.NewPassword,
            request.ConfirmPassword,
            cancellationToken);
        return ToActionResult(result);
    }

>>>>>>> Stashed changes
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(rawId, out var userId))
        {
            return Unauthorized(new ApiErrorResponse("Token không hợp lệ."));
        }

        var result = await _authService.GetCurrentUserAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("staff-only")]
    [Authorize(
    Roles = RoleNames.Admin + "," +
            RoleNames.Staff + "," +
            RoleNames.Coordinator)]
    public IActionResult StaffOnly()
    {
        return Ok(new
        {
            message = "Đã xác thực quyền nhân viên hoặc quản trị viên."
        });
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        return result.Succeeded
            ? StatusCode(result.StatusCode, result.Value)
            : StatusCode(result.StatusCode, new ApiErrorResponse(result.Error ?? "Đã xảy ra lỗi."));
    }
}
