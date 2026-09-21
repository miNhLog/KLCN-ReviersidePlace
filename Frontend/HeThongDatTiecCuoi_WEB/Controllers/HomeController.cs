using HeThongDatTiecCuoi_WEB.Constants;
using HeThongDatTiecCuoi_WEB.Models.Home;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class HomeController : Controller
{
    private readonly IRiversideApiClient _apiClient;

    public HomeController(IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Index", "AdminHall");
        }

        if (User.IsInRole(RoleNames.Consultant) || User.IsInRole(RoleNames.Coordinator))
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var result = await _apiClient.GetFeaturedHallsAsync(cancellationToken);
        return View(new HomeViewModel
        {
            FeaturedHalls = result.Succeeded && result.Value is not null
                ? result.Value
                : []
        });
    }


    [Authorize]
    public IActionResult Dashboard()
    {
        // Admin truy cập trực tiếp /Home/Dashboard
        // cũng chuyển về trang quản lý sảnh
        if (User.IsInRole(RoleNames.Admin))
        {
            return RedirectToAction("Index", "AdminHall");
        }

        return View();
    }


    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
