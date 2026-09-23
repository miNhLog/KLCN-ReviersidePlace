using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.DTOs.Booking;

namespace HeThongDatTiecCuoi_API.Controllers;

[Route("api/admin/bookings")]
[ApiController]
public class AdminBookingController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminBookingController(ApplicationDbContext context)
    {
        _context = context;
    }

    // 1. GET: api/admin/bookings - Danh sách tiệc cưới có phân trang & bộ lọc
    [HttpGet]
    public async Task<IActionResult> GetBookings([FromQuery] BookingFilterRequestDto filter)
    {
        var query = _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.Hall)
            .AsNoTracking();

        // Lọc theo từ khóa (Mã tiệc hoặc Tên khách hàng)
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(b => b.BookingCode.ToLower().Contains(kw)
                || (b.Customer != null && b.Customer.FullName.ToLower().Contains(kw)));
        }

        // Lọc theo trạng thái
        if (!string.IsNullOrWhiteSpace(filter.Status) && filter.Status != "ALL")
        {
            query = query.Where(b => b.Status == filter.Status);
        }

        // Lọc theo sảnh
        if (filter.HallId.HasValue && filter.HallId.Value > 0)
        {
            query = query.Where(b => b.HallSchedule != null && b.HallSchedule.HallId == filter.HallId.Value);
        }

        // Lọc theo khoảng ngày tổ chức
        if (filter.FromDate.HasValue)
        {
            query = query.Where(b => b.HallSchedule != null && b.HallSchedule.Date >= filter.FromDate.Value.Date);
        }
        if (filter.ToDate.HasValue)
        {
            query = query.Where(b => b.HallSchedule != null && b.HallSchedule.Date <= filter.ToDate.Value.Date);
        }

        // Đếm thống kê nhanh theo trạng thái trên toàn bộ dữ liệu
        var allBookings = await _context.WeddingBookings.AsNoTracking().ToListAsync();
        var pendingCount = allBookings.Count(b => b.Status == "Chờ xác nhận");
        var confirmedCount = allBookings.Count(b => b.Status == "Đã xác nhận");
        var depositedCount = allBookings.Count(b => b.Status == "Đã cọc");
        var completedCount = allBookings.Count(b => b.Status == "Hoàn tất");
        var cancelledCount = allBookings.Count(b => b.Status == "Đã hủy");

        var totalCount = await query.CountAsync();

        // Phân trang và sắp xếp tiệc mới nhất lên đầu
        var items = await query
            .OrderByDescending(b => b.BookedAt ?? DateTime.MinValue)
            .Skip((filter.PageIndex - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(b => new BookingSummaryDto
            {
                BookingId = b.BookingId,
                BookingCode = b.BookingCode,
                CustomerName = b.Customer != null ? b.Customer.FullName : $"Khách hàng #{b.CustomerId}",
                CustomerPhone = b.Customer != null ? (b.Customer.PhoneNumber ?? "") : "",
                HallName = b.HallSchedule != null && b.HallSchedule.Hall != null ? b.HallSchedule.Hall.HallName : "Chưa chọn sảnh",
                EventDate = b.HallSchedule != null ? b.HallSchedule.Date : (b.BookedAt ?? DateTime.Now),
                Shift = b.HallSchedule != null ? b.HallSchedule.Shift : "Ca tối",
                TableCount = b.TableCount ?? 0,
                EstimatedTotal = b.EstimatedTotal ?? 0m,
                Status = b.Status,
                BookedAt = b.BookedAt
            })
            .ToListAsync();

        return Ok(new BookingListResponseDto
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = filter.PageIndex,
            PageSize = filter.PageSize,
            PendingConfirmationCount = pendingCount,
            ConfirmedCount = confirmedCount,
            DepositedCount = depositedCount,
            CompletedCount = completedCount,
            CancelledCount = cancelledCount
        });
    }

    // 2. GET: api/admin/bookings/{id} - Xem chi tiết đơn đặt tiệc
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBookingDetail(int id)
    {
        var booking = await _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.HallSchedule)
                .ThenInclude(hs => hs!.Hall)
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            return NotFound(new { message = $"Không tìm thấy đơn tiệc với mã ID {id}" });
        }

        var detail = new BookingDetailDto
        {
            BookingId = booking.BookingId,
            BookingCode = booking.BookingCode,
            CustomerId = booking.CustomerId,
            CustomerName = booking.Customer != null ? booking.Customer.FullName : $"Khách hàng #{booking.CustomerId}",
            CustomerPhone = booking.Customer != null ? (booking.Customer.PhoneNumber ?? "") : "",
            CustomerEmail = string.Empty,
            HallId = booking.HallSchedule?.HallId,
            HallName = booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc",
            HallCode = booking.HallSchedule?.Hall?.HallCode ?? "",
            EventDate = booking.HallSchedule != null ? booking.HallSchedule.Date : (booking.BookedAt ?? DateTime.Now),
            Shift = booking.HallSchedule != null ? booking.HallSchedule.Shift : "Ca tối",
            MenuId = booking.MenuId,
            MenuName = booking.MenuId.HasValue ? $"Thực đơn Set #{booking.MenuId.Value}" : "Chưa chọn thực đơn",
            DecorationPackageId = booking.DecorationPackageId,
            DecorationPackageName = booking.DecorationPackageId.HasValue ? $"Gói Decor #{booking.DecorationPackageId.Value}" : "Gói Decor tiêu chuẩn",
            GuestCount = booking.GuestCount ?? 0,
            TableCount = booking.TableCount ?? 0,
            ExpectedBudget = booking.ExpectedBudget ?? 0m,
            DesiredStyle = booking.DesiredStyle ?? "Tiêu chuẩn hoàng gia",
            SpecialRequests = booking.SpecialRequests ?? "Không có",
            FinalMenuPrice = booking.FinalMenuPrice ?? 0m,
            FinalDecorationPrice = booking.FinalDecorationPrice ?? 0m,
            FinalHallPrice = booking.FinalHallPrice ?? 0m,
            EstimatedTotal = booking.EstimatedTotal ?? 0m,
            Status = booking.Status,
            CancellationReason = booking.CancellationReason,
            BookedAt = booking.BookedAt,
            UpdatedAt = booking.UpdatedAt
        };

        return Ok(detail);
    }

    // 3. POST: api/admin/bookings - Tạo mới đơn đặt tiệc & cập nhật lịch sảnh
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        // Kiểm tra khách hàng tồn tại
        var customerExists = await _context.Customers.AnyAsync(c => c.CustomerId == request.CustomerId);
        if (!customerExists)
        {
            return BadRequest(new { message = "Khách hàng không tồn tại trong hệ thống." });
        }

        // Kiểm tra sảnh tiệc tồn tại
        var hall = await _context.Halls.FindAsync(request.HallId);
        if (hall == null)
        {
            return BadRequest(new { message = "Sảnh tiệc không tồn tại." });
        }

        // Kiểm tra lịch sảnh xem ca đó có trống không
        var existingSchedule = await _context.HallSchedules
            .FirstOrDefaultAsync(ls => ls.HallId == request.HallId
                && ls.Date.Date == request.EventDate.Date
                && ls.Shift == request.Shift);

        if (existingSchedule != null && (existingSchedule.StatusId == 402 || existingSchedule.StatusId == 403))
        {
            return BadRequest(new { message = $"Sảnh {hall.HallName} đã được đặt hoặc tạm khóa vào {request.Shift} ngày {request.EventDate:dd/MM/yyyy}." });
        }

        // Nếu chưa có lịch sảnh thì tạo mới, nếu có và đang trống (401) thì cập nhật sang Đã đặt (402)
        int hallScheduleId;
        if (existingSchedule == null)
        {
            var newSchedule = new HallSchedule
            {
                HallId = request.HallId,
                Date = request.EventDate.Date,
                Shift = request.Shift,
                StatusId = 402 // Đã đặt
            };
            _context.HallSchedules.Add(newSchedule);
            await _context.SaveChangesAsync();
            hallScheduleId = newSchedule.HallScheduleId;
        }
        else
        {
            existingSchedule.StatusId = 402;
            hallScheduleId = existingSchedule.HallScheduleId;
        }

        // Tự động sinh mã tiệc theo định dạng: DT-yyyyMMdd-XXXX
        var randomCode = new Random().Next(1000, 9999);
        var bookingCode = $"DT-{request.EventDate:yyyyMMdd}-{randomCode}";

        // Tạm tính chi phí dự kiến nếu chưa có giá chốt cụ thể
        decimal menuPrice = 4500000m;
        decimal hallPrice = 30000000m;
        decimal decorPrice = 50000000m;
        decimal total = (menuPrice * request.TableCount) + hallPrice + decorPrice;

        var booking = new WeddingBooking
        {
            BookingCode = bookingCode,
            CustomerId = request.CustomerId,
            HallScheduleId = hallScheduleId,
            MenuId = request.MenuId,
            DecorationPackageId = request.DecorationPackageId,
            GuestCount = request.GuestCount,
            TableCount = request.TableCount,
            ExpectedBudget = request.ExpectedBudget ?? total,
            DesiredStyle = request.DesiredStyle ?? "Hoàng gia",
            SpecialRequests = request.SpecialRequests,
            FinalMenuPrice = menuPrice,
            FinalHallPrice = hallPrice,
            FinalDecorationPrice = decorPrice,
            EstimatedTotal = total,
            Status = "Chờ xác nhận",
            BookedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _context.WeddingBookings.Add(booking);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Tạo đơn đặt tiệc thành công.", bookingId = booking.BookingId, bookingCode });
    }

    // 4. PUT: api/admin/bookings/{id}/status - Duyệt đơn, Ghi nhận cọc, Hoàn tất, hoặc Hủy tiệc
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusRequestDto request)
    {
        var booking = await _context.WeddingBookings
            .Include(b => b.HallSchedule)
            .FirstOrDefaultAsync(b => b.BookingId == id);

        if (booking == null)
        {
            return NotFound(new { message = "Không tìm thấy đơn tiệc cưới." });
        }

        booking.Status = request.NewStatus;
        booking.UpdatedAt = DateTime.Now;

        // Nếu trạng thái là HỦY: cập nhật lý do hủy và GIẢI PHÓNG LỊCH SẢNH (chuyển về 401: Trống)
        if (request.NewStatus == "Đã hủy")
        {
            booking.CancellationReason = request.CancellationReason ?? "Hủy theo yêu cầu khách hàng";
            if (booking.HallSchedule != null)
            {
                booking.HallSchedule.StatusId = 401; // Trống (Sẵn sàng nhận tiệc khác)
            }
        }
        else if (request.NewStatus == "Đã xác nhận" || request.NewStatus == "Đã cọc")
        {
            if (booking.HallSchedule != null)
            {
                booking.HallSchedule.StatusId = 402; // Đã đặt
            }
        }

        await _context.SaveChangesAsync();
        return Ok(new { success = true, message = $"Đã cập nhật trạng thái tiệc sang '{request.NewStatus}'." });
    }
}