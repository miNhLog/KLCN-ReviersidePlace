using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Common;
using HeThongDatTiecCuoi_API.DTOs.Menu;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/menus")]
[Authorize(Roles = RoleNames.Admin)]
public sealed class MenuController : ControllerBase
{
    private static readonly string[] ValidStatuses =
    [
        MenuStatusCodes.Active,
        MenuStatusCodes.Inactive
    ];

    private readonly ApplicationDbContext _context;
    private readonly IStatusService _statusService;

    public MenuController(ApplicationDbContext context, IStatusService statusService)
    {
        _context = context;
        _statusService = statusService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMenus(
        [FromQuery] string? keyword,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = _context.Menus.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim();
            query = query.Where(menu =>
                menu.MenuCode.Contains(normalizedKeyword) ||
                menu.MenuName.Contains(normalizedKeyword));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(menu => menu.Status.StatusCode == normalizedStatus);
        }

        var menus = await query
            .OrderBy(menu => menu.MenuCode)
            .Select(menu => new MenuDto
            {
                MenuId = menu.MenuId,
                MenuCode = menu.MenuCode,
                MenuName = menu.MenuName,
                Description = menu.Description,
                PricePerTable = menu.PricePerTable,
                Status = menu.Status.StatusCode,
                StatusName = menu.Status.StatusName
            })
            .ToListAsync(cancellationToken);

        return Ok(menus);
    }

    [HttpPost]
    public async Task<IActionResult> CreateMenu(
        [FromBody] CreateMenuRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MenuName))
        {
            return BadRequest(new { message = "Tên thực đơn không được để trống." });
        }

        if (request.PricePerTable < 0)
        {
            return BadRequest(new { message = "Giá mỗi bàn không hợp lệ." });
        }

        var menuCodes = await _context.Menus
            .AsNoTracking()
            .Select(menu => menu.MenuCode)
            .ToListAsync(cancellationToken);

        var highestNumber = menuCodes
            .Where(code =>
                code.StartsWith("TD") &&
                int.TryParse(code[2..], out _))
            .Select(code => int.Parse(code[2..]))
            .DefaultIfEmpty(0)
            .Max();

        var activeStatus = await _statusService.GetStatusAsync(
            StatusGroups.Menu,
            MenuStatusCodes.Active,
            cancellationToken) ?? throw new InvalidOperationException("Thiếu trạng thái thực đơn ACTIVE.");

        var menu = new Menu
        {
            MenuCode = $"TD{highestNumber + 1:D3}",
            MenuName = request.MenuName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? null
                : request.Description.Trim(),
            PricePerTable = request.PricePerTable,
            StatusId = activeStatus.StatusId
        };

        _context.Menus.Add(menu);
        await _context.SaveChangesAsync(cancellationToken);

        return Created($"/api/menus/{menu.MenuId}", ToDto(menu, activeStatus));
    }

    [HttpPut("{menuId:int}")]
    public async Task<IActionResult> UpdateMenu(
        int menuId,
        [FromBody] UpdateMenuRequest request,
        CancellationToken cancellationToken)
    {
        var menu = await _context.Menus
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.MenuId == menuId, cancellationToken);

        if (menu is null)
        {
            return NotFound(new { message = "Không tìm thấy thực đơn." });
        }

        if (string.IsNullOrWhiteSpace(request.MenuName))
        {
            return BadRequest(new { message = "Tên thực đơn không được để trống." });
        }

        if (request.PricePerTable < 0)
        {
            return BadRequest(new { message = "Giá mỗi bàn không hợp lệ." });
        }

        menu.MenuName = request.MenuName.Trim();
        menu.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        menu.PricePerTable = request.PricePerTable;

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(ToDto(menu));
    }

    [HttpPatch("{menuId:int}/status")]
    public async Task<IActionResult> UpdateMenuStatus(
        int menuId,
        [FromBody] StatusRequest request,
        CancellationToken cancellationToken)
    {
        var menu = await _context.Menus
            .Include(item => item.Status)
            .FirstOrDefaultAsync(item => item.MenuId == menuId, cancellationToken);

        if (menu is null)
        {
            return NotFound(new { message = "Không tìm thấy thực đơn." });
        }

        var statusCode = request.Status?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(statusCode) || !ValidStatuses.Contains(statusCode))
        {
            return BadRequest(new { message = "Trạng thái thực đơn không hợp lệ." });
        }

        var status = await _statusService.GetStatusAsync(
            StatusGroups.Menu,
            statusCode,
            cancellationToken);
        if (status is null)
        {
            return BadRequest(new { message = "Trạng thái thực đơn chưa được cấu hình." });
        }

        menu.StatusId = status.StatusId;
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToDto(menu, status));
    }

    [HttpGet("{menuId:int}/dishes")]
    public async Task<IActionResult> GetMenuDishes(
        int menuId,
        CancellationToken cancellationToken)
    {
        if (!await MenuExistsAsync(menuId, cancellationToken))
        {
            return NotFound(new { message = "Không tìm thấy thực đơn." });
        }

        var dishes = await _context.MenuDishes
            .AsNoTracking()
            .Where(menuDish => menuDish.MenuId == menuId)
            .OrderBy(menuDish => menuDish.SortOrder)
            .Select(menuDish => new MenuDishDto
            {
                MenuDishId = menuDish.MenuDishId,
                DishId = menuDish.DishId,
                DishCode = menuDish.Dish.DishCode,
                DishName = menuDish.Dish.DishName,
                Category = menuDish.Dish.Category,
                ImageUrl = menuDish.Dish.ImageUrl,
                Status = menuDish.Dish.Status.StatusCode,
                StatusName = menuDish.Dish.Status.StatusName,
                SortOrder = menuDish.SortOrder
            })
            .ToListAsync(cancellationToken);

        return Ok(dishes);
    }

    [HttpPost("{menuId:int}/dishes")]
    public async Task<IActionResult> AddDishToMenu(
        int menuId,
        [FromBody] AddDishToMenuRequest request,
        CancellationToken cancellationToken)
    {
        if (!await MenuExistsAsync(menuId, cancellationToken))
        {
            return NotFound(new { message = "Không tìm thấy thực đơn." });
        }

        if (await IsMenuInUseAsync(menuId, cancellationToken))
        {
            return Conflict(new
            {
                message = "Thực đơn đã được sử dụng trong đặt tiệc nên không thể thay đổi thành phần món."
            });
        }

        var dish = await _context.Dishes
            .Include(item => item.Status)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.DishId == request.DishId, cancellationToken);

        if (dish is null)
        {
            return NotFound(new { message = "Không tìm thấy món ăn." });
        }

        if (dish.Status.StatusCode != DishStatusCodes.Active)
        {
            return Conflict(new { message = "Chỉ có thể thêm món đang phục vụ vào thực đơn." });
        }

        var alreadyExists = await _context.MenuDishes
            .AsNoTracking()
            .AnyAsync(
                menuDish => menuDish.MenuId == menuId && menuDish.DishId == request.DishId,
                cancellationToken);

        if (alreadyExists)
        {
            return Conflict(new { message = "Món ăn đã tồn tại trong thực đơn." });
        }

        var lastSortOrder = await _context.MenuDishes
            .Where(menuDish => menuDish.MenuId == menuId)
            .Select(menuDish => (int?)menuDish.SortOrder)
            .MaxAsync(cancellationToken) ?? 0;

        var menuDish = new MenuDish
        {
            MenuId = menuId,
            DishId = request.DishId,
            SortOrder = lastSortOrder + 1
        };

        _context.MenuDishes.Add(menuDish);
        await _context.SaveChangesAsync(cancellationToken);

        var response = new MenuDishDto
        {
            MenuDishId = menuDish.MenuDishId,
            DishId = dish.DishId,
            DishCode = dish.DishCode,
            DishName = dish.DishName,
            Category = dish.Category,
            ImageUrl = dish.ImageUrl,
            Status = dish.Status.StatusCode,
            StatusName = dish.Status.StatusName,
            SortOrder = menuDish.SortOrder
        };

        return Created($"/api/menus/{menuId}/dishes/{dish.DishId}", response);
    }

    [HttpDelete("{menuId:int}/dishes/{dishId:int}")]
    public async Task<IActionResult> RemoveDishFromMenu(
        int menuId,
        int dishId,
        CancellationToken cancellationToken)
    {
        if (!await MenuExistsAsync(menuId, cancellationToken))
        {
            return NotFound(new { message = "Không tìm thấy thực đơn." });
        }

        if (await IsMenuInUseAsync(menuId, cancellationToken))
        {
            return Conflict(new
            {
                message = "Thực đơn đã được sử dụng trong đặt tiệc nên không thể thay đổi thành phần món."
            });
        }

        var menuDish = await _context.MenuDishes
            .FirstOrDefaultAsync(
                item => item.MenuId == menuId && item.DishId == dishId,
                cancellationToken);

        if (menuDish is null)
        {
            return NotFound(new { message = "Món ăn không tồn tại trong thực đơn này." });
        }

        var removedSortOrder = menuDish.SortOrder;
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        _context.MenuDishes.Remove(menuDish);
        await _context.SaveChangesAsync(cancellationToken);

        await _context.MenuDishes
            .Where(item => item.MenuId == menuId && item.SortOrder > removedSortOrder)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    item => item.SortOrder,
                    item => item.SortOrder - 1),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Ok(new
        {
            message = "Bỏ món khỏi thực đơn thành công.",
            menuId,
            dishId
        });
    }

    [HttpPut("{menuId:int}/dishes/order")]
    public async Task<IActionResult> ReorderMenuDishes(
        int menuId,
        [FromBody] ReorderMenuDishesRequest request,
        CancellationToken cancellationToken)
    {
        if (!await MenuExistsAsync(menuId, cancellationToken))
        {
            return NotFound(new { message = "Không tìm thấy thực đơn." });
        }

        if (await IsMenuInUseAsync(menuId, cancellationToken))
        {
            return Conflict(new
            {
                message = "Thực đơn đã được sử dụng trong đặt tiệc nên không thể thay đổi thành phần món."
            });
        }

        if (request.DishIds.Count == 0 ||
            request.DishIds.Distinct().Count() != request.DishIds.Count)
        {
            return BadRequest(new { message = "Danh sách món ăn không hợp lệ." });
        }

        var currentDishIds = await _context.MenuDishes
            .AsNoTracking()
            .Where(menuDish => menuDish.MenuId == menuId)
            .Select(menuDish => menuDish.DishId)
            .ToListAsync(cancellationToken);

        if (currentDishIds.Count != request.DishIds.Count ||
            !request.DishIds.All(currentDishIds.Contains))
        {
            return BadRequest(new { message = "Danh sách món ăn không khớp với thực đơn hiện tại." });
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            await _context.MenuDishes
                .Where(menuDish => menuDish.MenuId == menuId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        menuDish => menuDish.SortOrder,
                        menuDish => -menuDish.MenuDishId),
                    cancellationToken);

            var menuDishes = await _context.MenuDishes
                .Where(menuDish => menuDish.MenuId == menuId)
                .ToListAsync(cancellationToken);

            for (var index = 0; index < request.DishIds.Count; index++)
            {
                var dishId = request.DishIds[index];
                menuDishes.First(item => item.DishId == dishId).SortOrder = index + 1;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Ok(new { message = "Sắp xếp món ăn trong thực đơn thành công." });
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private Task<bool> MenuExistsAsync(int menuId, CancellationToken cancellationToken) =>
        _context.Menus
            .AsNoTracking()
            .AnyAsync(menu => menu.MenuId == menuId, cancellationToken);

    private async Task<bool> IsMenuInUseAsync(
        int menuId,
        CancellationToken cancellationToken)
    {
        var bookingCount = await _context.Database
            .SqlQuery<int>($"""
                SELECT COUNT(*) AS [Value]
                FROM DatTiec
                WHERE ThucDonID = {menuId}
                """)
            .SingleAsync(cancellationToken);

        return bookingCount > 0;
    }

    private static MenuDto ToDto(Menu menu, Status? status = null) => new()
    {
        MenuId = menu.MenuId,
        MenuCode = menu.MenuCode,
        MenuName = menu.MenuName,
        Description = menu.Description,
        PricePerTable = menu.PricePerTable,
        Status = (status ?? menu.Status).StatusCode,
        StatusName = (status ?? menu.Status).StatusName
    };

}
