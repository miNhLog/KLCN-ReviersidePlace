using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.Models;
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

    public HallScheduleController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("weekly")]
    public async Task<IActionResult> GetWeeklyHallSchedules(
        DateTime startDate,
        int? hallId = null)
    {
        var weekStartDate = startDate.Date;
        var weekEndDate = weekStartDate.AddDays(6);

        var hallsQuery = _context.Halls.AsQueryable();

        if (hallId.HasValue)
        {
            hallsQuery = hallsQuery
                .Where(hall => hall.HallId == hallId.Value);
        }

        var halls = await hallsQuery
            .OrderBy(hall => hall.HallId)
            .ToListAsync();

        if (halls.Count == 0)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh."
            });
        }

        await EnsureWeeklySchedulesAsync(halls, weekStartDate);

        var hallSchedules = await _context.HallSchedules
            .Include(schedule => schedule.Hall)
            .Where(schedule =>
                schedule.Date >= weekStartDate &&
                schedule.Date <= weekEndDate &&
                (!hallId.HasValue || schedule.HallId == hallId.Value))
            .OrderBy(schedule => schedule.HallId)
            .ThenBy(schedule => schedule.Date)
            .ThenBy(schedule => schedule.Shift)
            .ToListAsync();

        var bookings = await _context.DatTiec
            .Include(booking => booking.KhachHang)
            .Where(booking =>
                booking.HallSchedule.Date >= weekStartDate &&
                booking.HallSchedule.Date <= weekEndDate &&
                booking.TrangThai != "Đã hủy")
            .ToListAsync();

        var scheduleGroups = halls.Select(hall => new
        {
            hallId = hall.HallId,
            hallCode = hall.HallCode,
            hallName = hall.HallName,
            hallStatus = hall.Status,
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
                            : item.Status,
                        notes = item.Notes,
                        booking = booking is null
                            ? null
                            : new
                            {
                                bookingId = booking.DatTiecID,
                                bookingCode = booking.MaDatTiec,
                                customerName = booking.KhachHang.HoTen,
                                tableCount = booking.SoBan,
                                guestCount = booking.SoLuongKhach,
                                bookingStatus = booking.TrangThai
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
        [FromBody] StatusRequest request)
    {
        var status = request.Status?.Trim();

        var hallSchedule = await _context.HallSchedules.FindAsync(hallScheduleId);

        if (hallSchedule is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy lịch sảnh."
            });
        }

        var hasBooking = await _context.DatTiec
            .AnyAsync(booking =>
                booking.HallScheduleId == hallScheduleId &&
                booking.TrangThai != "Đã hủy");

        if (hasBooking)
        {
            return BadRequest(new
            {
                message = "Không thể chỉnh trạng thái vì ca này đã có booking."
            });
        }

        if (status != HallScheduleStatusCodes.Available &&
            status != HallScheduleStatusCodes.Locked)
        {
            return BadRequest(new
            {
                message = "Admin chỉ được chuyển trạng thái giữa Trống và Tạm khóa."
            });
        }

        hallSchedule.Status = status;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật trạng thái lịch sảnh thành công.",
            hallSchedule
        });
    }

    private async Task EnsureWeeklySchedulesAsync(
        List<Hall> halls,
        DateTime startDate)
    {
        var endDate = startDate.AddDays(6);

        var existingSchedules = await _context.HallSchedules
            .Where(schedule =>
                schedule.Date >= startDate &&
                schedule.Date <= endDate)
            .ToListAsync();

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
                        Status = HallScheduleStatusCodes.Available
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
