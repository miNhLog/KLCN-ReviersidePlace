using System.Security.Claims;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Banner;
using HeThongDatTiecCuoi_API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeThongDatTiecCuoi_API.Controllers;

[ApiController]
[Route("api/banners")]
public sealed class BannersController : ControllerBase
{
    private const long MaxImageSize = 5 * 1024 * 1024;
    private static readonly HashSet<string> ActionTypes = ["NONE", "INTERNAL", "SECTION", "AI"];
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _environment;

    public BannersController(ApplicationDbContext db, IWebHostEnvironment environment)
    {
        _db = db;
        _environment = environment;
    }

    [AllowAnonymous, HttpGet("public")]
    public async Task<ActionResult<List<BannerDto>>> GetPublic(CancellationToken ct) =>
        Ok((await _db.HomeBanners.AsNoTracking()
            .Where(x => x.IsVisible && x.ImagePath != null && x.ImagePath != "")
            .OrderBy(x => x.SortOrder).ThenBy(x => x.BannerId).ToListAsync(ct)).Select(ToDto));

    [Authorize(Roles = RoleNames.Manager), HttpGet]
    public async Task<ActionResult<List<BannerDto>>> GetAll(CancellationToken ct) =>
        Ok((await _db.HomeBanners.AsNoTracking().OrderBy(x => x.SortOrder).ThenBy(x => x.BannerId)
            .ToListAsync(ct)).Select(ToDto));

    [Authorize(Roles = RoleNames.Manager), HttpGet("{id:int}")]
    public async Task<ActionResult<BannerDto>> Get(int id, CancellationToken ct)
    {
        var banner = await _db.HomeBanners.AsNoTracking().SingleOrDefaultAsync(x => x.BannerId == id, ct);
        return banner is null ? NotFound(new { message = "Không tìm thấy banner." }) : Ok(ToDto(banner));
    }

    [Authorize(Roles = RoleNames.Manager), HttpPost]
    public async Task<ActionResult<BannerDto>> Create(BannerUpsertRequest request, CancellationToken ct)
    {
        if (!ValidateActions(request, out var error)) return BadRequest(new { message = error });
        var maxOrder = await _db.HomeBanners.MaxAsync(x => (int?)x.SortOrder, ct) ?? 0;
        var banner = new HomeBanner { CreatedAt = DateTime.Now, CreatedByUserId = UserId(), IsVisible = false };
        Apply(banner, request);
        banner.IsVisible = false; // Draft until an image is uploaded and Manager explicitly publishes it.
        banner.SortOrder = Math.Min(Math.Max(1, request.SortOrder), maxOrder + 1);
        await ShiftForInsert(banner.SortOrder, ct);
        _db.HomeBanners.Add(banner);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = banner.BannerId }, ToDto(banner));
    }

    [Authorize(Roles = RoleNames.Manager), HttpPut("{id:int}")]
    public async Task<ActionResult<BannerDto>> Update(int id, BannerUpsertRequest request, CancellationToken ct)
    {
        if (!ValidateActions(request, out var error)) return BadRequest(new { message = error });
        var banner = await _db.HomeBanners.SingleOrDefaultAsync(x => x.BannerId == id, ct);
        if (banner is null) return NotFound(new { message = "Không tìm thấy banner." });
        if (!VersionMatches(banner, request.Version)) return Conflict(new { message = "Banner vừa được người khác cập nhật. Vui lòng tải lại trang." });
        if (request.IsVisible && string.IsNullOrWhiteSpace(banner.ImagePath))
            return BadRequest(new { message = "Vui lòng tải ảnh trước khi hiển thị banner." });
        Apply(banner, request);
        banner.UpdatedAt = DateTime.Now;
        banner.UpdatedByUserId = UserId();
        await _db.SaveChangesAsync(ct);
        await NormalizeOrders(ct);
        return Ok(ToDto(banner));
    }

    [Authorize(Roles = RoleNames.Manager), HttpPost("{id:int}/image")]
    [RequestSizeLimit(MaxImageSize + 1024)]
    public async Task<ActionResult<BannerDto>> UploadImage(int id, IFormFile? image, CancellationToken ct)
    {
        var banner = await _db.HomeBanners.SingleOrDefaultAsync(x => x.BannerId == id, ct);
        if (banner is null) return NotFound(new { message = "Không tìm thấy banner." });
        var validation = await ValidateImage(image, ct);
        if (validation.Error is not null) return BadRequest(new { message = validation.Error });

        var directory = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "banners");
        Directory.CreateDirectory(directory);
        var fileName = $"{Guid.NewGuid():N}{validation.Extension}";
        var fullPath = Path.Combine(directory, fileName);
        await using (var output = System.IO.File.Create(fullPath)) await image!.CopyToAsync(output, ct);

        var oldPath = banner.ImagePath;
        banner.ImagePath = $"/uploads/banners/{fileName}";
        banner.UpdatedAt = DateTime.Now;
        banner.UpdatedByUserId = UserId();
        try { await _db.SaveChangesAsync(ct); }
        catch { System.IO.File.Delete(fullPath); throw; }

        DeleteUploadedFile(oldPath);
        return Ok(ToDto(banner));
    }

    [Authorize(Roles = RoleNames.Manager), HttpPut("reorder")]
    public async Task<IActionResult> Reorder(BannerReorderRequest request, CancellationToken ct)
    {
        var banners = await _db.HomeBanners.OrderBy(x => x.SortOrder).ThenBy(x => x.BannerId).ToListAsync(ct);
        if (request.BannerIds.Count != banners.Count || request.BannerIds.Distinct().Count() != banners.Count ||
            banners.Any(x => !request.BannerIds.Contains(x.BannerId)))
            return BadRequest(new { message = "Danh sách sắp xếp phải chứa chính xác tất cả banner." });
        for (var i = 0; i < request.BannerIds.Count; i++)
        {
            var banner = banners.Single(x => x.BannerId == request.BannerIds[i]);
            banner.SortOrder = i + 1;
            banner.UpdatedAt = DateTime.Now;
            banner.UpdatedByUserId = UserId();
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Đã cập nhật thứ tự banner." });
    }

    [Authorize(Roles = RoleNames.Manager), HttpPatch("{id:int}/visibility")]
    public async Task<IActionResult> Visibility(int id, BannerVisibilityRequest request, CancellationToken ct)
    {
        var banner = await _db.HomeBanners.SingleOrDefaultAsync(x => x.BannerId == id, ct);
        if (banner is null) return NotFound(new { message = "Không tìm thấy banner." });
        if (!string.IsNullOrWhiteSpace(request.Version) && !VersionMatches(banner, request.Version))
            return Conflict(new { message = "Banner vừa được cập nhật. Vui lòng tải lại trang." });
        if (request.IsVisible && string.IsNullOrWhiteSpace(banner.ImagePath))
            return BadRequest(new { message = "Vui lòng tải ảnh đại diện lên trước khi bật hiển thị banner." });
        banner.IsVisible = request.IsVisible;
        banner.UpdatedAt = DateTime.Now;
        banner.UpdatedByUserId = UserId();
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = request.IsVisible ? "Banner đã được hiển thị." : "Banner đã được ẩn." });
    }

    [Authorize(Roles = RoleNames.Manager), HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var banner = await _db.HomeBanners.SingleOrDefaultAsync(x => x.BannerId == id, ct);
        if (banner is null) return NotFound(new { message = "Không tìm thấy banner." });
        var oldPath = banner.ImagePath;
        _db.HomeBanners.Remove(banner);
        await _db.SaveChangesAsync(ct);
        await NormalizeOrders(ct);
        DeleteUploadedFile(oldPath);
        return Ok(new { message = "Đã xóa banner." });
    }

    [AllowAnonymous, HttpGet("images/{fileName}")]
    [ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
    public IActionResult Image(string fileName)
    {
        if (fileName != Path.GetFileName(fileName)) return BadRequest();
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".webp" => "image/webp", _ => null };
        if (contentType is null) return NotFound();
        var path = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "banners", fileName);
        return System.IO.File.Exists(path) ? PhysicalFile(path, contentType) : NotFound();
    }

    private async Task ShiftForInsert(int order, CancellationToken ct)
    {
        var following = await _db.HomeBanners.Where(x => x.SortOrder >= order).ToListAsync(ct);
        foreach (var item in following) item.SortOrder++;
    }

    private async Task NormalizeOrders(CancellationToken ct)
    {
        var items = await _db.HomeBanners.OrderBy(x => x.SortOrder).ThenBy(x => x.BannerId).ToListAsync(ct);
        for (var i = 0; i < items.Count; i++) items[i].SortOrder = i + 1;
        await _db.SaveChangesAsync(ct);
    }

    private static void Apply(HomeBanner target, BannerUpsertRequest source)
    {
        target.Title = source.Title.Trim(); target.Subtitle = source.Subtitle.Trim();
        target.Description = source.Description.Trim(); target.NavigationLabel = source.NavigationLabel.Trim();
        target.ImageAlt = source.ImageAlt.Trim();
        target.PrimaryActionType = string.IsNullOrWhiteSpace(source.PrimaryActionType) ? "NONE" : source.PrimaryActionType.Trim().ToUpperInvariant();
        target.PrimaryButtonLabel = Clean(source.PrimaryButtonLabel); target.PrimaryLink = Clean(source.PrimaryLink);
        target.SecondaryActionType = string.IsNullOrWhiteSpace(source.SecondaryActionType) ? "NONE" : source.SecondaryActionType.Trim().ToUpperInvariant();
        target.SecondaryButtonLabel = Clean(source.SecondaryButtonLabel); target.SecondaryLink = Clean(source.SecondaryLink);
        target.SortOrder = source.SortOrder; target.IsVisible = source.IsVisible;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool ValidateActions(BannerUpsertRequest request, out string error)
    {
        request.PrimaryActionType = string.IsNullOrWhiteSpace(request.PrimaryActionType) ? "NONE" : request.PrimaryActionType.Trim().ToUpperInvariant();
        request.SecondaryActionType = string.IsNullOrWhiteSpace(request.SecondaryActionType) ? "NONE" : request.SecondaryActionType.Trim().ToUpperInvariant();
        foreach (var action in new[] { (request.PrimaryActionType, request.PrimaryButtonLabel, request.PrimaryLink), (request.SecondaryActionType, request.SecondaryButtonLabel, request.SecondaryLink) })
        {
            var type = action.Item1;
            if (!ActionTypes.Contains(type)) { error = "Loại CTA không hợp lệ."; return false; }
            if (type != "NONE" && string.IsNullOrWhiteSpace(action.Item2)) { error = "CTA phải có nhãn nút."; return false; }
            if (type == "SECTION" && (string.IsNullOrWhiteSpace(action.Item3) || !action.Item3.StartsWith('#'))) { error = "Liên kết section phải bắt đầu bằng #."; return false; }
            if (type == "INTERNAL" && (string.IsNullOrWhiteSpace(action.Item3) || !action.Item3.StartsWith('/') || action.Item3.StartsWith("//"))) { error = "Liên kết nội bộ phải bắt đầu bằng một dấu /."; return false; }
        }
        error = string.Empty; return true;
    }

    private static async Task<(string? Extension, string? Error)> ValidateImage(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return (null, "Vui lòng chọn ảnh.");
        if (file.Length > MaxImageSize) return (null, "Ảnh không được vượt quá 5 MB.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) return (null, "Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.");
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header, ct);
        var valid = ext is ".jpg" or ".jpeg" ? read >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff
            : ext == ".png" ? read >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            : read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
        return valid ? (ext == ".jpeg" ? ".jpg" : ext, null) : (null, "Nội dung tệp không đúng định dạng ảnh đã khai báo.");
    }

    private void DeleteUploadedFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("/uploads/banners/", StringComparison.Ordinal)) return;
        var fileName = Path.GetFileName(path);
        var fullPath = Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "uploads", "banners", fileName);
        if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);
    }

    private int? UserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private static bool VersionMatches(HomeBanner banner, string? version) => string.IsNullOrWhiteSpace(version) || Convert.ToBase64String(banner.RowVersion) == version;
    private static BannerDto ToDto(HomeBanner x) => new(x.BannerId, x.Title, x.Subtitle, x.Description,
        x.NavigationLabel, x.ImagePath, x.ImageAlt, x.PrimaryActionType, x.PrimaryButtonLabel, x.PrimaryLink,
        x.SecondaryActionType, x.SecondaryButtonLabel, x.SecondaryLink, x.SortOrder, x.IsVisible,
        x.CreatedAt, x.UpdatedAt, Convert.ToBase64String(x.RowVersion));
}
