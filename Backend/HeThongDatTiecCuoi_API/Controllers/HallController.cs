using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/halls")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class HallController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HallController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetHalls()
    {
        var halls = await _context.Halls
            .OrderBy(hall => hall.HallId)
            .ToListAsync();

        return Ok(halls);
    }

    [HttpGet("{hallId:int}")]
    public async Task<IActionResult> GetHall(int hallId)
    {
        var hall = await _context.Halls.FindAsync(hallId);

        if (hall is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        return Ok(hall);
    }

    [HttpPost]
    public async Task<IActionResult> CreateHall(Hall hall)
    {
        if (string.IsNullOrWhiteSpace(hall.HallCode))
        {
            return BadRequest(new
            {
                message = "Mã sảnh không được để trống."
            });
        }

        if (string.IsNullOrWhiteSpace(hall.HallName))
        {
            return BadRequest(new
            {
                message = "Tên sảnh không được để trống."
            });
        }

        var hallCodeExists = await _context.Halls
            .AnyAsync(existingHall => existingHall.HallCode == hall.HallCode);

        if (hallCodeExists)
        {
            return BadRequest(new
            {
                message = "Mã sảnh đã tồn tại."
            });
        }

        if (hall.MaximumCapacity <= 0)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (hall.MinimumCapacity.HasValue &&
            hall.MinimumCapacity.Value > hall.MaximumCapacity)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (hall.RentalPrice < 0)
        {
            return BadRequest(new
            {
                message = "Giá thuê không hợp lệ."
            });
        }

        if (hall.Status != "Hoạt động" &&
            hall.Status != "Bảo trì" &&
            hall.Status != "Ngừng hoạt động")
        {
            return BadRequest(new
            {
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        _context.Halls.Add(hall);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetHall),
            new { hallId = hall.HallId },
            hall);
    }

    [HttpPut("{hallId:int}")]
    public async Task<IActionResult> UpdateHall(int hallId, Hall hall)
    {
        var existingHall = await _context.Halls.FindAsync(hallId);

        if (existingHall is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        var hallCodeExists = await _context.Halls
            .AnyAsync(otherHall =>
                otherHall.HallCode == hall.HallCode &&
                otherHall.HallId != hallId);

        if (hallCodeExists)
        {
            return BadRequest(new
            {
                message = "Mã sảnh đã tồn tại."
            });
        }

        if (hall.MaximumCapacity <= 0)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối đa phải lớn hơn 0."
            });
        }

        if (hall.MinimumCapacity.HasValue &&
            hall.MinimumCapacity.Value > hall.MaximumCapacity)
        {
            return BadRequest(new
            {
                message = "Sức chứa tối thiểu không được lớn hơn sức chứa tối đa."
            });
        }

        if (hall.RentalPrice < 0)
        {
            return BadRequest(new
            {
                message = "Giá thuê không hợp lệ."
            });
        }

        if (hall.Status != "Hoạt động" &&
            hall.Status != "Bảo trì" &&
            hall.Status != "Ngừng hoạt động")
        {
            return BadRequest(new
            {
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        existingHall.HallCode = hall.HallCode;
        existingHall.HallName = hall.HallName;
        existingHall.MinimumCapacity = hall.MinimumCapacity;
        existingHall.MaximumCapacity = hall.MaximumCapacity;
        existingHall.RentalPrice = hall.RentalPrice;
        existingHall.Description = hall.Description;
        existingHall.ImageUrl = hall.ImageUrl;
        existingHall.Status = hall.Status;

        await _context.SaveChangesAsync();

        return Ok(existingHall);
    }

    [HttpPatch("{hallId:int}/status")]
    public async Task<IActionResult> UpdateHallStatus(
        int hallId,
        [FromBody] string status)
    {
        var hall = await _context.Halls.FindAsync(hallId);

        if (hall is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        if (status != "Hoạt động" &&
            status != "Bảo trì" &&
            status != "Ngừng hoạt động")
        {
            return BadRequest(new
            {
                message = "Trạng thái sảnh không hợp lệ."
            });
        }

        hall.Status = status;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Cập nhật trạng thái thành công.",
            hall
        });
    }

    [HttpDelete("{hallId:int}")]
    public async Task<IActionResult> DeleteHall(int hallId)
    {
        var hall = await _context.Halls.FindAsync(hallId);

        if (hall is null)
        {
            return NotFound(new
            {
                message = "Không tìm thấy sảnh tiệc."
            });
        }

        if (hall.Status == "Hoạt động")
        {
            return BadRequest(new
            {
                message = "Không thể xóa sảnh đang hoạt động. Hãy chuyển sảnh sang trạng thái Ngừng hoạt động trước."
            });
        }

        var hasBooking = await _context.DatTiec
            .AnyAsync(booking => booking.HallSchedule.HallId == hallId);

        if (hasBooking)
        {
            return BadRequest(new
            {
                message = "Không thể xóa sảnh vì sảnh đã từng có booking."
            });
        }

        var hallSchedules = await _context.HallSchedules
            .Where(schedule => schedule.HallId == hallId)
            .ToListAsync();

        _context.HallSchedules.RemoveRange(hallSchedules);
        _context.Halls.Remove(hall);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            message = "Xóa sảnh thành công."
        });
    }
}
