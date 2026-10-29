using System.Security.Claims;
using HeThongDatTiecCuoi_WEB.Models.Payment;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("Payment")]
public sealed class PaymentController : Controller
{
    private readonly IRiversideApiClient _apiClient;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IRiversideApiClient apiClient, ILogger<PaymentController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Proxy tạo liên kết thanh toán VietQR PayOS đặt cọc 10%.
    /// </summary>
    [HttpPost("CreateDeposit")]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositPaymentRequestViewModel request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || request.BookingId <= 0)
                return Json(new { success = false, message = "Thiếu thông tin đơn tiệc cần thanh toán cọc." });

            string? accessToken = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                accessToken = Request.Cookies["access_token"] ?? User.FindFirstValue("access_token");
            }

            var result = await _apiClient.CreateDepositPaymentAsync(request, accessToken, cancellationToken);
            if (result.Succeeded && result.Value != null)
            {
                return Json(new { success = true, data = result.Value });
            }

            return Json(new { success = false, message = result.Error ?? "Không thể khởi tạo mã VietQR lúc này." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi proxy tạo thanh toán cọc cho BookingId={BookingId}", request?.BookingId);
            return Json(new { success = false, message = "Không thể kết nối đến cổng thanh toán, vui lòng thử lại sau." });
        }
    }

    /// <summary>
    /// Proxy tạo liên kết thanh toán VietQR PayOS cho hợp đồng (Đợt 2 / Thanh toán từ xa).
    /// </summary>
    [HttpPost("CreateContractPayment")]
    public async Task<IActionResult> CreateContractPayment([FromBody] CreateCustomerContractPaymentRequestViewModel request, CancellationToken cancellationToken)
    {
        try
        {
            if (request == null || request.BookingId <= 0)
                return Json(new { success = false, message = "Thiếu thông tin đơn tiệc cần thanh toán." });

            string? accessToken = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                accessToken = Request.Cookies["access_token"] ?? User.FindFirstValue("access_token");
            }

            var result = await _apiClient.CreateCustomerContractPaymentAsync(request, accessToken, cancellationToken);
            if (result.Succeeded && result.Value != null)
            {
                return Json(new { success = true, data = result.Value });
            }

            return Json(new { success = false, message = result.Error ?? "Không thể khởi tạo mã VietQR lúc này." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi proxy tạo thanh toán hợp đồng cho BookingId={BookingId}", request?.BookingId);
            return Json(new { success = false, message = "Không thể kết nối đến cổng thanh toán, vui lòng thử lại sau." });
        }
    }

    /// <summary>
    /// Proxy kiểm tra trạng thái thanh toán phục vụ Polling mỗi 2 giây.
    /// </summary>
    [HttpGet("CheckStatus")]
    public async Task<IActionResult> CheckStatus([FromQuery] long orderCode, CancellationToken cancellationToken)
    {
        try
        {
            if (orderCode <= 0)
                return Json(new { success = false, message = "Mã giao dịch không hợp lệ." });

            var result = await _apiClient.CheckPaymentStatusAsync(orderCode, cancellationToken);
            if (result.Succeeded && result.Value != null)
            {
                return Json(new { success = true, data = result.Value });
            }

            return Json(new { success = false, message = result.Error ?? "Không thể kiểm tra trạng thái thanh toán." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi proxy kiểm tra trạng thái OrderCode={OrderCode}", orderCode);
            return Json(new { success = false, message = "Lỗi kết nối kiểm tra giao dịch." });
        }
    }

    /// <summary>
    /// Trang Callback khi khách bấm trở lại từ cổng thanh toán PayOS (ReturnUrl).
    /// </summary>
    [HttpGet("Success")]
    public IActionResult Success([FromQuery] string? code, [FromQuery] string? id, [FromQuery] string? cancel, [FromQuery] string? status, [FromQuery] long? orderCode)
    {
        TempData["PaymentNotification"] = "Giao dịch cọc của bạn đã được tiếp nhận và xử lý!";
        return RedirectToAction("Index", "MyBooking");
    }

    /// <summary>
    /// Trang Callback khi khách bấm hủy giao dịch trên PayOS (CancelUrl).
    /// </summary>
    [HttpGet("Cancel")]
    public IActionResult Cancel([FromQuery] long? orderCode)
    {
        TempData["PaymentWarning"] = "Bạn đã hủy quá trình thanh toán cọc VietQR.";
        return RedirectToAction("Index", "MyBooking");
    }
}
