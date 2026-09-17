using HeThongDatTiecCuoi_WEB.Constants.StatusCodes;
using HeThongDatTiecCuoi_WEB.Models.AdminThucDon;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Authorize(Roles = "Quản trị viên")]
public sealed class AdminThucDonController : Controller
{
    private const string ApiTokenCookie = "rp_api_token";

    private readonly IRiversideApiClient _apiClient;

    public AdminThucDonController(
        IRiversideApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // =========================================================
    // INDEX
    // =========================================================

    [HttpGet]
    public async Task<IActionResult> Index(
        string? tuKhoaThucDon,
        string? trangThaiThucDon,
        string? tuKhoaMonAn,
        string? nhomMon,
        string? trangThaiMonAn,
        int? thucDonId,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var model = new QuanLyThucDonViewModel
        {
            TuKhoaThucDon = tuKhoaThucDon,
            TrangThaiThucDon = trangThaiThucDon,
            TuKhoaMonAn = tuKhoaMonAn,
            NhomMon = nhomMon,
            TrangThaiMonAn = trangThaiMonAn,
            ThucDonDangChonID = thucDonId
        };

        var ketQuaThucDon =
            await _apiClient.GetDanhSachThucDonAsync(
                tuKhoaThucDon,
                trangThaiThucDon,
                accessToken,
                cancellationToken);

        if (ketQuaThucDon.Succeeded &&
            ketQuaThucDon.Value is not null)
        {
            model.DanhSachThucDon =
                ketQuaThucDon.Value;
        }
        else
        {
            ModelState.AddModelError(
                string.Empty,
                ketQuaThucDon.Error
                ?? "Không thể tải danh sách thực đơn.");
        }

        var ketQuaMonAn =
            await _apiClient.GetDanhSachMonAnAsync(
                tuKhoaMonAn,
                nhomMon,
                trangThaiMonAn,
                accessToken,
                cancellationToken);

        if (ketQuaMonAn.Succeeded &&
            ketQuaMonAn.Value is not null)
        {
            model.DanhSachMonAn =
                ketQuaMonAn.Value;
        }
        else
        {
            ModelState.AddModelError(
                string.Empty,
                ketQuaMonAn.Error
                ?? "Không thể tải danh sách món ăn.");
        }

        // Danh sách riêng cho dropdown thêm món.
        var monDangPhucVu =
            await _apiClient.GetDanhSachMonAnAsync(
                null,
                null,
                DishStatusCodes.Active,
                accessToken,
                cancellationToken);

        if (monDangPhucVu.Succeeded &&
            monDangPhucVu.Value is not null)
        {
            model.DanhSachMonDangPhucVu =
                monDangPhucVu.Value;
        }

        if (!model.ThucDonDangChonID.HasValue)
        {
            model.ThucDonDangChonID =
                model.DanhSachThucDon
                    .FirstOrDefault()
                    ?.ThucDonID;
        }

        if (model.ThucDonDangChonID.HasValue)
        {
            var chiTiet =
                await _apiClient.GetMonAnTrongThucDonAsync(
                    model.ThucDonDangChonID.Value,
                    accessToken,
                    cancellationToken);

            if (chiTiet.Succeeded &&
                chiTiet.Value is not null)
            {
                model.DanhSachMonTrongThucDon =
                    chiTiet.Value;
            }
            else if (!chiTiet.Succeeded)
            {
                ModelState.AddModelError(
                    string.Empty,
                    chiTiet.Error
                    ?? "Không thể tải chi tiết thực đơn.");
            }
        }

        return View(model);
    }


    // =========================================================
    // CRUD THỰC ĐƠN
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoThucDon(
        TaoThucDonForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "Vui lòng kiểm tra lại thông tin thực đơn.";

            return RedirectToAction(nameof(Index));
        }

        var result =
            await _apiClient.TaoThucDonAsync(
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded ||
            result.Value is null)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể tạo thực đơn.";

            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] =
            "Thêm thực đơn thành công.";

        return RedirectToAction(
            nameof(Index),
            new
            {
                thucDonId = result.Value.ThucDonID
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatThucDon(
        CapNhatThucDonForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.CapNhatThucDonAsync(
                model.ThucDonID,
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể cập nhật thực đơn.";
        }
        else
        {
            TempData["Success"] =
                "Cập nhật thực đơn thành công.";
        }

        return RedirectToAction(
            nameof(Index),
            new
            {
                thucDonId = model.ThucDonID
            });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThaiThucDon(
        int thucDonId,
        string trangThai,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.CapNhatTrangThaiThucDonAsync(
                thucDonId,
                trangThai,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Cập nhật trạng thái thực đơn thành công."
                : result.Error
                  ?? "Không thể cập nhật trạng thái thực đơn.";

        return RedirectToAction(
            nameof(Index),
            new { thucDonId });
    }


    // =========================================================
    // CRUD MÓN ĂN
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TaoMonAn(
        TaoMonAnForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "Vui lòng kiểm tra lại thông tin món ăn.";

            return RedirectToAction(nameof(Index));
        }

        var result =
            await _apiClient.TaoMonAnAsync(
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded ||
            result.Value is null)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể thêm món ăn.";

            return RedirectToAction(nameof(Index));
        }

        if (model.HinhAnh is not null &&
            model.HinhAnh.Length > 0)
        {
            await using var stream =
                model.HinhAnh.OpenReadStream();

            var uploadResult =
                await _apiClient.UploadHinhAnhMonAnAsync(
                    result.Value.MonAnID,
                    stream,
                    model.HinhAnh.FileName,
                    model.HinhAnh.ContentType,
                    accessToken,
                    cancellationToken);

            if (!uploadResult.Succeeded)
            {
                TempData["Error"] =
                    "Đã tạo món nhưng tải ảnh thất bại: "
                    + uploadResult.Error;

                return RedirectToAction(nameof(Index));
            }
        }

        TempData["Success"] =
            "Thêm món ăn thành công.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatMonAn(
        CapNhatMonAnForm model,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.CapNhatMonAnAsync(
                model.MonAnID,
                model,
                accessToken,
                cancellationToken);

        if (!result.Succeeded)
        {
            TempData["Error"] =
                result.Error
                ?? "Không thể cập nhật món ăn.";

            return RedirectToAction(nameof(Index));
        }

        if (model.HinhAnhMoi is not null &&
            model.HinhAnhMoi.Length > 0)
        {
            await using var stream =
                model.HinhAnhMoi.OpenReadStream();

            var uploadResult =
                await _apiClient.UploadHinhAnhMonAnAsync(
                    model.MonAnID,
                    stream,
                    model.HinhAnhMoi.FileName,
                    model.HinhAnhMoi.ContentType,
                    accessToken,
                    cancellationToken);

            if (!uploadResult.Succeeded)
            {
                TempData["Error"] =
                    "Đã sửa thông tin món nhưng tải ảnh thất bại: "
                    + uploadResult.Error;

                return RedirectToAction(nameof(Index));
            }
        }
        else if (model.XoaHinhAnh)
        {
            var deleteImageResult =
                await _apiClient.XoaHinhAnhMonAnAsync(
                    model.MonAnID,
                    accessToken,
                    cancellationToken);

            if (!deleteImageResult.Succeeded)
            {
                TempData["Error"] =
                    deleteImageResult.Error
                    ?? "Không thể xóa hình ảnh món ăn.";

                return RedirectToAction(nameof(Index));
            }
        }

        TempData["Success"] =
            "Cập nhật món ăn thành công.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CapNhatTrangThaiMonAn(
        int monAnId,
        string trangThai,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.CapNhatTrangThaiMonAnAsync(
                monAnId,
                trangThai,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Cập nhật trạng thái món ăn thành công."
                : result.Error
                  ?? "Không thể cập nhật trạng thái món ăn.";

        return RedirectToAction(nameof(Index));
    }


    // =========================================================
    // CHI TIẾT THỰC ĐƠN
    // =========================================================

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThemMonVaoThucDon(
        int thucDonId,
        int monAnId,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.ThemMonVaoThucDonAsync(
                thucDonId,
                monAnId,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Thêm món vào thực đơn thành công."
                : result.Error
                  ?? "Không thể thêm món vào thực đơn.";

        return RedirectToAction(
            nameof(Index),
            new { thucDonId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> XoaMonKhoiThucDon(
        int thucDonId,
        int monAnId,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var result =
            await _apiClient.XoaMonKhoiThucDonAsync(
                thucDonId,
                monAnId,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Bỏ món khỏi thực đơn thành công."
                : result.Error
                  ?? "Không thể bỏ món khỏi thực đơn.";

        return RedirectToAction(
            nameof(Index),
            new { thucDonId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SapXepMonAn(
        int thucDonId,
        string danhSachMonAnID,
        CancellationToken cancellationToken)
    {
        var accessToken = GetAccessToken();

        if (accessToken is null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var ids =
            (danhSachMonAnID ?? string.Empty)
                .Split(
                    ',',
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(x =>
                    int.TryParse(x, out var id)
                        ? id
                        : 0)
                .Where(x => x > 0)
                .ToList();

        if (ids.Count == 0)
        {
            TempData["Error"] =
                "Danh sách sắp xếp không hợp lệ.";

            return RedirectToAction(
                nameof(Index),
                new { thucDonId });
        }

        var result =
            await _apiClient.SapXepMonAnAsync(
                thucDonId,
                ids,
                accessToken,
                cancellationToken);

        TempData[result.Succeeded
            ? "Success"
            : "Error"] =
            result.Succeeded
                ? "Sắp xếp món ăn thành công."
                : result.Error
                  ?? "Không thể sắp xếp món ăn.";

        return RedirectToAction(
            nameof(Index),
            new { thucDonId });
    }


    // =========================================================
    // HELPER
    // =========================================================

    private string? GetAccessToken() =>
        Request.Cookies[ApiTokenCookie];
}
