using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index() =>
        User.Identity?.IsAuthenticated == true
            ? RedirectToAction(nameof(Dashboard))
            : RedirectToAction("Login", "Auth");

    [Authorize]
    public IActionResult Dashboard() => View();

    [HttpGet("khong-co-quyen")]
    public IActionResult AccessDenied() => View();
}
