using HeThongDatTiecCuoi_WEB.Models.Menus;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("menus")]
[AllowAnonymous]
public sealed class MenusController(IRiversideApiClient apiClient) : Controller
{
    private const int PageSize = 20;

    [HttpGet("")]
    public async Task<IActionResult> Index(int? menuId, CancellationToken cancellationToken = default)
    {
        var dishesTask = apiClient.GetPublicDishesAsync(null, null, cancellationToken);
        var listTask = apiClient.GetPublicMenusAsync(null, null, "name-asc", 1, PageSize, cancellationToken);
        await Task.WhenAll(dishesTask, listTask);
        var dishes = await dishesTask;
        var result = await listTask;
        var response = result.Value;
        var menus = response?.Items.ToList() ?? [];

        if (result.Succeeded && response is not null)
        {
            for (var page = 2; page <= response.TotalPages; page++)
            {
                result = await apiClient.GetPublicMenusAsync(null, null, "name-asc", page, PageSize, cancellationToken);
                if (!result.Succeeded || result.Value is null) break;
                menus.AddRange(result.Value.Items);
            }
        }

        var listSucceeded = result.Succeeded && result.Value is not null;
        MenuGallerySetViewModel[] sets = [];
        if (listSucceeded)
        {
            // Bound simultaneous requests while loading each set's actual dish membership.
            using var gate = new SemaphoreSlim(6);
            sets = await Task.WhenAll(menus
                .OrderBy(menu => menu.MenuCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(menu => menu.MenuId)
                .Select(async menu =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        var detail = await apiClient.GetPublicMenuDetailAsync(menu.MenuId, cancellationToken);
                        return new MenuGallerySetViewModel(menu,
                            detail.Succeeded && detail.Value is not null
                                ? detail.Value.Dishes.OrderBy(dish => dish.SortOrder).ToArray() : [],
                            detail.Succeeded && detail.Value is not null
                                ? null : "Không thể tải các món trong thực đơn này. Vui lòng thử lại sau.");
                    }
                    finally { gate.Release(); }
                }));
        }

        return View(new MenuGalleryViewModel
        {
            Dishes = dishes.Succeeded ? dishes.Value ?? [] : [],
            Menus = sets,
            SelectedMenuId = menuId,
            DishesError = dishes.Succeeded && dishes.Value is not null ? null : "Không thể tải danh sách món ăn. Vui lòng thử lại sau.",
            MenusError = listSucceeded ? null : "Không thể tải danh sách thực đơn. Vui lòng thử lại sau."
        });
    }

    // Existing bookmarks now select the set on the gallery instead of opening a detail page.
    [HttpGet("{menuId:int}")]
    public IActionResult Details(int menuId) =>
        Redirect(Url.Action(nameof(Index), new { menuId }) + "#special-set-menu");
}
