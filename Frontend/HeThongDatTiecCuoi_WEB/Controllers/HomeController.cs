using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Dashboard))
            : RedirectToAction("Login", "Auth");

<<<<<<< Updated upstream
    [Authorize]
    public IActionResult Dashboard() => View();
=======
    [Authorize(
        Roles = RoleNames.Admin + "," +
                RoleNames.Consultant + "," +
                RoleNames.Coordinator)]
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

>>>>>>> Stashed changes

    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied() => View();
}
