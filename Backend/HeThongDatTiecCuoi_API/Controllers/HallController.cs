using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.DTOs.Hall;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/halls")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class HallController : ControllerBase
{
    private static readonly string[] ValidStatuses =
    [
        HallStatusCodes.Active,
        HallStatusCodes.Maintenance,
        HallStatusCodes.Inactive
    ];

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;

    public HallController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpGet("featured")]
    [AllowAnonymous]
    public async Task<IActionResult> GetFeaturedHalls(CancellationToken cancellationToken)
    {
        var halls = await _context.Halls.AsNoTracking()
            .Where(hall => hall.Status.StatusCode == HallStatusCodes.Active)
            .OrderBy(hall => hall.HallId)
            .Take(3)
            .Select(hall => new HallDto
            {
                HallId = hall.HallId,
                HallCode = hall.HallCode,
                HallName = hall.HallName,
                MinimumCapacity = hall.MinimumCapacity,
                MaximumCapacity = hall.MaximumCapacity,
                RentalPrice = hall.RentalPrice,
                Description = hall.Description,
                ImageUrl = hall.ImageUrl,
                Status = hall.Status.StatusCode,
                StatusName = hall.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        return Ok(halls);
    }

    [HttpGet]
    public async Task<IActionResult> GetHalls(CancellationToken cancellationToken)
    {
        var halls = await _context.Halls.AsNoTracking()
            .Include(hall => hall.Status)
            .OrderBy(hall => hall.HallId)
            .ToListAsync(cancellationToken);
        return Ok(halls.Select(hall => ToDto(hall)));
    }

    [HttpGet("{hallId:int}")]
    public async Task<IActionResult> GetHall(int hallId, CancellationToken cancellationToken)
    {
        var hall = await _context.Halls.AsNoTracking()
            .Include(item => item.Status)
            .Where(item => item.HallId == hallId)
            .SingleOrDefaultAsync(cancellationToken);
        return hall is null
            ? NotFound(new { message = "Không tìm thấy sảnh tiệc." })
            : Ok(ToDto(hall));
    }

    [HttpPost]
    public async Task<IActionResult> CreateHall(
        [FromBody] CreateHallRequest request,
        CancellationToken cancellationToken)
    {
        var error = Validate(request.HallCode, request.HallName, request.MinimumCapacity,
            request.MaximumCapacity, request.RentalPrice);
        if (error is not null) return BadRequest(new { message = error });

        if (await _context.Halls.AnyAsync(
                hall => hall.HallCode == request.HallCode.Trim(), cancellationToken))
            return Conflict(new { message = "Mã sảnh đã tồn tại." });

        var status = await ResolveStatusAsync(request.Status, cancellationToken);
        if (status is null) return BadRequest(new { message = "Trạng thái sảnh không hợp lệ." });

        var hall = new Hall
        {
            HallCode = request.HallCode.Trim(),
            HallName = request.HallName.Trim(),
            MinimumCapacity = request.MinimumCapacity,
            MaximumCapacity = request.MaximumCapacity,
            RentalPrice = request.RentalPrice,
            Description = request.Description?.Trim(),
            ImageUrl = request.ImageUrl?.Trim(),
            StatusId = status.StatusId
        };
        _context.Halls.Add(hall);
        await _context.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetHall), new { hallId = hall.HallId }, ToDto(hall, status));
    }

    [HttpPut("{hallId:int}")]
    public async Task<IActionResult> UpdateHall(
        int hallId,
        [FromBody] UpdateHallRequest request,
        CancellationToken cancellationToken)
    {
        var hall = await _context.Halls.Include(item => item.Status)
            .SingleOrDefaultAsync(item => item.HallId == hallId, cancellationToken);
        if (hall is null) return NotFound(new { message = "Không tìm thấy sảnh tiệc." });

        var error = Validate(request.HallCode, request.HallName, request.MinimumCapacity,
            request.MaximumCapacity, request.RentalPrice);
        if (error is not null) return BadRequest(new { message = error });

        if (await _context.Halls.AnyAsync(item =>
                item.HallCode == request.HallCode.Trim() && item.HallId != hallId,
                cancellationToken))
            return Conflict(new { message = "Mã sảnh đã tồn tại." });

        var status = await ResolveStatusAsync(request.Status, cancellationToken);
        if (status is null) return BadRequest(new { message = "Trạng thái sảnh không hợp lệ." });

        hall.HallCode = request.HallCode.Trim();
        hall.HallName = request.HallName.Trim();
        hall.MinimumCapacity = request.MinimumCapacity;
        hall.MaximumCapacity = request.MaximumCapacity;
        hall.RentalPrice = request.RentalPrice;
        hall.Description = request.Description?.Trim();
        hall.ImageUrl = request.ImageUrl?.Trim();
        hall.StatusId = status.StatusId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(hall, status));
    }

    [HttpPatch("{hallId:int}/status")]
    public async Task<IActionResult> UpdateHallStatus(
        int hallId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var hall = await _context.Halls.FindAsync([hallId], cancellationToken);
        if (hall is null) return NotFound(new { message = "Không tìm thấy sảnh tiệc." });

        var status = await ResolveStatusAsync(request.Status, cancellationToken);
        if (status is null) return BadRequest(new { message = "Trạng thái sảnh không hợp lệ." });

        hall.StatusId = status.StatusId;
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Cập nhật trạng thái thành công.", hall = ToDto(hall, status) });
    }

    [HttpDelete("{hallId:int}")]
    public async Task<IActionResult> DeleteHall(int hallId, CancellationToken cancellationToken)
    {
        var hall = await _context.Halls.Include(item => item.Status)
            .SingleOrDefaultAsync(item => item.HallId == hallId, cancellationToken);
        if (hall is null) return NotFound(new { message = "Không tìm thấy sảnh tiệc." });
        if (hall.Status.StatusCode == HallStatusCodes.Active)
            return Conflict(new { message = "Không thể xóa sảnh đang hoạt động. Hãy chuyển sảnh sang trạng thái Ngừng hoạt động trước." });

        if (await _context.WeddingBookings.AnyAsync(
                booking => booking.HallSchedule.HallId == hallId, cancellationToken))
            return Conflict(new { message = "Không thể xóa sảnh vì sảnh đã từng có booking." });

        var schedules = await _context.HallSchedules.Where(schedule => schedule.HallId == hallId)
            .ToListAsync(cancellationToken);
        _context.HallSchedules.RemoveRange(schedules);
        _context.Halls.Remove(hall);
        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { message = "Xóa sảnh thành công." });
    }

    private async Task<Status?> ResolveStatusAsync(string? code, CancellationToken cancellationToken)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalized) || !ValidStatuses.Contains(normalized)) return null;
        return await _statusService.GetStatusAsync(StatusGroups.Hall, normalized, cancellationToken);
    }

    private static string? Validate(string hallCode, string hallName, int? minimumCapacity,
        int maximumCapacity, decimal rentalPrice)
    {
        if (string.IsNullOrWhiteSpace(hallCode)) return "Mã sảnh không được để trống.";
        if (string.IsNullOrWhiteSpace(hallName)) return "Tên sảnh không được để trống.";
        if (maximumCapacity <= 0) return "Sức chứa tối đa phải lớn hơn 0.";
        if (minimumCapacity.HasValue && minimumCapacity.Value > maximumCapacity)
            return "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa.";
        return rentalPrice < 0 ? "Giá thuê không hợp lệ." : null;
    }

    private static HallDto ToDto(Hall hall, Status? status = null) => new()
    {
        HallId = hall.HallId,
        HallCode = hall.HallCode,
        HallName = hall.HallName,
        MinimumCapacity = hall.MinimumCapacity,
        MaximumCapacity = hall.MaximumCapacity,
        RentalPrice = hall.RentalPrice,
        Description = hall.Description,
        ImageUrl = hall.ImageUrl,
        Status = (status ?? hall.Status).StatusCode,
        StatusName = (status ?? hall.Status).StatusName
    };
}
