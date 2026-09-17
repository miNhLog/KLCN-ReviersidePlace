using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/hall-schedules")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class HallScheduleController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;

    public HallScheduleController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpGet("weekly")]
    public async Task<IActionResult> GetWeeklyHallSchedules(
        DateTime startDate,
        int? hallId = null,
        CancellationToken cancellationToken = default)
    {
        var weekStartDate = startDate.Date;
        var weekEndDate = weekStartDate.AddDays(6);

        var hallsQuery = _context.Halls.Include(hall => hall.Status).AsQueryable();

        if (hallId.HasValue)
        {
            hallsQuery = hallsQuery
                .Where(hall => hall.HallId == hallId.Value);
        }

        var halls = await hallsQuery
            .OrderBy(hall => hall.HallId)
            .ToListAsync(cancellationToken);

        if (halls.Count == 0)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh."
            });
        }

        await EnsureWeeklySchedulesAsync(halls, weekStartDate, cancellationToken);

        var hallSchedules = await _context.HallSchedules
            .Include(schedule => schedule.Hall)
            .Include(schedule => schedule.Status)
            .Where(schedule =>
                schedule.Date >= weekStartDate &&
                schedule.Date <= weekEndDate &&
                (!hallId.HasValue || schedule.HallId == hallId.Value))
            .OrderBy(schedule => schedule.HallId)
            .ThenBy(schedule => schedule.Date)
            .ThenBy(schedule => schedule.Shift)
            .ToListAsync(cancellationToken);

        var bookedStatus = await _statusService.GetStatusAsync(
            StatusGroups.HallSchedule,
            HallScheduleStatusCodes.Booked,
            cancellationToken) ?? throw new InvalidOperationException("Thiếu trạng thái lịch sảnh BOOKED.");

        var bookings = await _context.WeddingBookings
            .Include(booking => booking.Customer)
            .Where(booking =>
                booking.HallSchedule.Date >= weekStartDate &&
                booking.HallSchedule.Date <= weekEndDate &&
                booking.Status != "Đã hủy")
            .ToListAsync(cancellationToken);

        var scheduleGroups = halls.Select(hall => new
        {
            hallId = hall.HallId,
            hallCode = hall.HallCode,
            hallName = hall.HallName,
            hallStatus = hall.Status.StatusCode,
            hallStatusName = hall.Status.StatusName,
            schedule = hallSchedules
                .Where(item => item.HallId == hall.HallId)
                .Select(item =>
                {
                    var booking = bookings
                        .FirstOrDefault(existingBooking =>
                            existingBooking.HallScheduleId == item.HallScheduleId);

                    return new
                    {
                        hallScheduleId = item.HallScheduleId,
                        date = item.Date,
                        shift = item.Shift,
                        status = booking is not null
                            ? HallScheduleStatusCodes.Booked
                            : item.Status.StatusCode,
                        statusName = booking is not null
                            ? bookedStatus.StatusName
                            : item.Status.StatusName,
                        notes = item.Notes,
                        booking = booking is null
                            ? null
                            : new
                            {
                                bookingId = booking.BookingId,
                                bookingCode = booking.BookingCode,
                                customerName = booking.Customer.FullName,
                                tableCount = booking.TableCount,
                                guestCount = booking.GuestCount,
                                bookingStatus = booking.Status
                            }
                    };
                })
                .ToList()
        });

        return Ok(new
        {
            startDate = weekStartDate,
            endDate = weekEndDate,
            halls = scheduleGroups
        });
    }

    [HttpPatch("{hallScheduleId:int}/status")]
    public async Task<IActionResult> UpdateHallScheduleStatus(
        int hallScheduleId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var statusCode = request.Status?.Trim().ToUpperInvariant();

        var hallSchedule = await _context.HallSchedules.FindAsync([hallScheduleId], cancellationToken);

        if (hallSchedule is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy lịch sảnh."
            });
        }

        var hasBooking = await _context.WeddingBookings
            .AnyAsync(booking =>
                booking.HallScheduleId == hallScheduleId &&
                booking.Status != "Đã hủy",
                cancellationToken);

        if (hasBooking)
        {
            return BadRequest(new
            {
                message = "Không thể chỉnh trạng thái vì ca này đã có booking."
            });
        }

        if (statusCode != HallScheduleStatusCodes.Available &&
            statusCode != HallScheduleStatusCodes.Locked)
        {
            return BadRequest(new
            {
                message = "Admin chỉ được chuyển trạng thái giữa Trống và Tạm khóa."
            });
        }

        var status = await _statusService.GetStatusAsync(
            StatusGroups.HallSchedule,
            statusCode,
            cancellationToken) ?? throw new InvalidOperationException(
                $"Thiếu trạng thái lịch sảnh {statusCode}.");

        hallSchedule.StatusId = status.StatusId;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Cập nhật trạng thái lịch sảnh thành công.",
            hallScheduleId = hallSchedule.HallScheduleId,
            status = status.StatusCode,
            statusName = status.StatusName
        });
    }

    private async Task EnsureWeeklySchedulesAsync(
        List<Hall> halls,
        DateTime startDate,
        CancellationToken cancellationToken)
    {
        var endDate = startDate.AddDays(6);

        var existingSchedules = await _context.HallSchedules
            .Where(schedule =>
                schedule.Date >= startDate &&
                schedule.Date <= endDate)
            .ToListAsync(cancellationToken);

        var availableStatusId = await _statusService.GetStatusIdAsync(
            StatusGroups.HallSchedule,
            HallScheduleStatusCodes.Available,
            cancellationToken);

        var shifts = new[]
        {
            "Ca trưa",
            "Ca tối"
        };

        foreach (var hall in halls)
        {
            for (var dayOffset = 0; dayOffset < 7; dayOffset++)
            {
                var date = startDate.AddDays(dayOffset);

                foreach (var shift in shifts)
                {
                    var scheduleExists = existingSchedules.Any(schedule =>
                        schedule.HallId == hall.HallId &&
                        schedule.Date.Date == date.Date &&
                        schedule.Shift == shift);

                    if (scheduleExists)
                    {
                        continue;
                    }

                    _context.HallSchedules.Add(new HallSchedule
                    {
                        HallId = hall.HallId,
                        Date = date,
                        Shift = shift,
                        StatusId = availableStatusId
                    });
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
