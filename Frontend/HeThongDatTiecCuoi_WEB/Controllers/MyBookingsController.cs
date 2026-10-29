using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using HeThongDatTiecCuoi_WEB.Models.MyBookings;
using HeThongDatTiecCuoi_WEB.Services;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public class MyBookingsController : Controller
{
    private readonly IRiversideApiClient _apiClient;

    public MyBookingsController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? phone, [FromQuery] string? bookingCode, CancellationToken cancellationToken)
    {
        ViewData["PublicActivePage"] = "my-bookings";

        int? currentUserId = null;
        if (User.Identity?.IsAuthenticated == true)
        {
            var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idClaim, out var uid))
            {
                currentUserId = uid;
            }
        }

        // Nếu đã đăng nhập: lấy danh sách tiệc của chính tài khoản đó
        if (currentUserId.HasValue)
        {
            var result = await _apiClient.GetMyBookingsAsync(null, currentUserId, cancellationToken);
            var bookings = result.Value ?? new List<MyBookingViewModel>();
            var targetPhone = bookings.Count > 0 ? bookings[0].PhoneNumber : "";
            ViewBag.CurrentPhone = targetPhone;
            return View(bookings);
        }

        // SEC-03: Khách chưa đăng nhập không được truy cập danh sách chỉ qua số điện thoại
        // Chuyển hướng sang trang tra cứu /MyBooking với mã và số điện thoại
        if (!string.IsNullOrWhiteSpace(bookingCode) && !string.IsNullOrWhiteSpace(phone))
        {
            return RedirectToAction("Index", "MyBooking", new { bookingCode = bookingCode.Trim(), phone = phone.Trim() });
        }

        return RedirectToAction("Index", "MyBooking");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelBooking(
        int bookingId,
        [FromQuery] string? bookingCode,
        [FromQuery] string? phone,
        CancellationToken cancellationToken)
    {
        var targetPhone = phone ?? "";
        if (string.IsNullOrEmpty(targetPhone) && User.Identity?.IsAuthenticated == true)
            targetPhone = User.FindFirstValue(ClaimTypes.MobilePhone) ?? User.FindFirstValue("phone") ?? "";
        if (string.IsNullOrEmpty(targetPhone))
            targetPhone = Request.Cookies["customer_phone"] ?? "";

        var result = await _apiClient.CancelBookingAsync(bookingId, bookingCode, targetPhone, cancellationToken);
        if (result.Succeeded)
        {
            return Json(new { success = true, message = "Đã hủy đơn giữ chỗ tiệc cưới thành công." });
        }
        return Json(new { success = false, message = result.Error ?? "Không thể hủy đơn tiệc lúc này." });
    }

    [HttpGet("DownloadContractPdf")]
    public async Task<IActionResult> DownloadContractPdf(
        [FromQuery] string bookingCode,
        [FromQuery] string? phone,
        CancellationToken cancellationToken)
    {
        try
        {
            var targetPhone = phone ?? "";
            if (string.IsNullOrEmpty(targetPhone) && User.Identity?.IsAuthenticated == true)
                targetPhone = User.FindFirstValue(ClaimTypes.MobilePhone) ?? User.FindFirstValue("phone") ?? "";
            if (string.IsNullOrEmpty(targetPhone))
                targetPhone = Request.Cookies["customer_phone"] ?? "";

            var result = await _apiClient.DownloadPublicContractPdfAsync(bookingCode, targetPhone, cancellationToken);
            if (result.Succeeded && result.Value is not null)
            {
                return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
            }

            TempData["ErrorMessage"] = result.Error ?? "Không thể tải PDF hợp đồng.";
            return RedirectToAction(nameof(Index), new { phone = targetPhone });
        }
        catch
        {
            return RedirectToAction(nameof(Index));
        }
    }

}