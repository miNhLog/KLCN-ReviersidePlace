using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.DTOs.Booking;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/my-bookings")]
public class MyBookingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public MyBookingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: api/my-bookings?phone=0849485279&userId=1
    [HttpGet]
    public async Task<IActionResult> GetMyBookings([FromQuery] string? phone, [FromQuery] int? userId)
    {
        if (string.IsNullOrWhiteSpace(phone) && (!userId.HasValue || userId.Value <= 0))
        {
            return Ok(new { success = true, data = Array.Empty<MyBookingDto>() });
        }

        Customer? customer = null;

        // Ưu tiên tìm Khách hàng theo UserId của tài khoản đăng nhập
        if (userId.HasValue && userId.Value > 0)
        {
            customer = await _context.Set<Customer>()
                .FirstOrDefaultAsync(c => c.UserId == userId.Value);
        }

        // Nếu chưa tìm thấy theo UserId, tìm tiếp theo Số điện thoại
        if (customer == null && !string.IsNullOrWhiteSpace(phone))
        {
            var cleanPhone = phone.Trim();
            customer = await _context.Set<Customer>()
                .FirstOrDefaultAsync(c => c.PhoneNumber == cleanPhone);
        }

        if (customer == null)
        {
            return Ok(new { success = true, data = Array.Empty<MyBookingDto>() });
        }

        // Truy vấn danh sách tiệc cưới của khách hàng
        var bookings = await _context.Set<WeddingBooking>()
            .Where(b => b.CustomerId == customer.CustomerId)
            .OrderByDescending(b => b.BookedAt)
            .ToListAsync();

        var result = new List<MyBookingDto>();

        foreach (var b in bookings)
        {
            var schedule = b.HallScheduleId.HasValue
                ? await _context.HallSchedules.FindAsync(b.HallScheduleId.Value)
                : null;

            var hall = schedule != null
                ? await _context.Halls.FindAsync(schedule.HallId)
                : null;

            var menu = b.MenuId.HasValue
                ? await _context.Menus.FindAsync(b.MenuId.Value)
                : null;

            var decor = b.DecorationPackageId.HasValue
                ? await _context.DecorPackages.FindAsync(b.DecorationPackageId.Value)
                : null;

            var (consultantName, consultantPhone) = await GetConsultantInfoAsync(b.ConsultantEmployeeId);

            result.Add(new MyBookingDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerName = customer.FullName,
                PhoneNumber = customer.PhoneNumber,
                HallId = schedule?.HallId ?? 0,
                HallName = hall?.HallName ?? "Sảnh tiệc Riverside",
                EventDate = schedule?.Date ?? DateTime.Today,
                Shift = schedule?.Shift ?? "Ca tối",
                TableCount = b.TableCount ?? 20,
                GuestCount = b.GuestCount ?? 200,
                MenuName = menu?.MenuName ?? "Thực đơn tiệc cưới cao cấp",
                MenuPricePerTable = menu?.PricePerTable ?? 0,
                DecorName = decor?.TenGoi ?? "Gói Concept Sang Trọng",
                DecorPrice = decor?.Gia ?? 0,
                EstimatedTotal = b.EstimatedTotal ?? 0,
                Status = string.IsNullOrWhiteSpace(b.Status) ? "Chờ xác nhận" : b.Status,
                BookedAt = b.BookedAt ?? DateTime.Now,
                SpecialRequests = b.SpecialRequests,
                ConsultantName = consultantName,
                ConsultantPhone = consultantPhone
            });
        }

        return Ok(new { success = true, data = result });
    }

    // PUT: api/my-bookings/{id}/cancel
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> CancelBooking(int id)
    {
        var booking = await _context.Set<WeddingBooking>().FindAsync(id);
        if (booking == null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy đơn tiệc cưới này." });
        }

        if (booking.Status != "Chờ xác nhận")
        {
            return BadRequest(new { success = false, message = "Chỉ có thể hủy đơn tiệc đang ở trạng thái 'Chờ xác nhận'. Vui lòng liên hệ trực tiếp lễ tân nếu đã đặt cọc." });
        }

        booking.Status = "Đã hủy";
        booking.UpdatedAt = DateTime.Now;

        // Trả lại trạng thái trống cho Lịch sảnh nếu có
        if (booking.HallScheduleId.HasValue)
        {
            var schedule = await _context.HallSchedules.FindAsync(booking.HallScheduleId.Value);
            if (schedule != null)
            {
                schedule.StatusId = 401; // Sẵn sàng đón tiệc khác
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = "Đã hủy đơn giữ chỗ tiệc cưới thành công." });
    }

    // Hàm phụ trợ: Lấy thông tin họ tên & SĐT từ bảng dbo.NhanVien mà không bị lỗi DB Context
    private async Task<(string Name, string Phone)> GetConsultantInfoAsync(int? consultantId)
    {
        string defaultName = "Chuyên viên tư vấn Riverside";
        string defaultPhone = "0909 123 456";

        if (!consultantId.HasValue || consultantId.Value <= 0)
        {
            return (defaultName, defaultPhone);
        }

        try
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
            {
                await conn.OpenAsync();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT TOP 1 * FROM dbo.NhanVien WHERE NhanVienID = @id";

            var param = cmd.CreateParameter();
            param.ParameterName = "@id";
            param.Value = consultantId.Value;
            cmd.Parameters.Add(param);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    var col = reader.GetName(i).ToLower();
                    if (col.Contains("ten") || col.Contains("name"))
                    {
                        var val = reader.GetValue(i)?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) defaultName = val;
                    }
                    else if (col.Contains("thoai") || col.Contains("phone") || col.Contains("sdt"))
                    {
                        var val = reader.GetValue(i)?.ToString();
                        if (!string.IsNullOrWhiteSpace(val)) defaultPhone = val;
                    }
                }
            }
        }
        catch
        {
            // Dự phòng giữ thông tin liên hệ mặc định của nhà hàng
        }

        return (defaultName, defaultPhone);
    }
}