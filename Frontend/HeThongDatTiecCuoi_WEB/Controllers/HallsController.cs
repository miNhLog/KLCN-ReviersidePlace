using HeThongDatTiecCuoi_WEB.Models.Halls;
using HeThongDatTiecCuoi_WEB.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("halls")]
public sealed class HallsController : Controller
{
    private const int PageSize = 20;
    private readonly IRiversideApiClient _apiClient;
    private readonly IWebHostEnvironment _environment;

    public HallsController(IRiversideApiClient apiClient, IWebHostEnvironment environment)
    {
        _apiClient = apiClient;
        _environment = environment;
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index(CancellationToken cancellationToken = default)
    {
        var result = await _apiClient.GetPublicHallsAsync(
            null, null, null, "name-asc", 1, PageSize, cancellationToken);
        var response = result.Value;
        var halls = response?.Items.ToList() ?? [];

        // Keep the complete collection available without filter or pagination controls.
        if (result.Succeeded && response is not null)
        {
            for (var page = 2; page <= response.TotalPages; page++)
            {
                result = await _apiClient.GetPublicHallsAsync(
                    null, null, null, "name-asc", page, PageSize, cancellationToken);
                if (!result.Succeeded || result.Value is null) break;
                halls.AddRange(result.Value.Items);
            }
        }

        return View(new HallListViewModel
        {
            Halls = halls,
            PageSize = PageSize,
            TotalItems = halls.Count,
            TotalPages = halls.Count > 0 ? 1 : 0,
            ErrorMessage = result.Succeeded && result.Value is not null
                ? null
                : result.Error ?? "Không thể tải danh sách sảnh. Vui lòng thử lại sau."
        });
    }

    [HttpGet("{hallId:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Details(
        int hallId,
        CancellationToken cancellationToken)
    {
        var result = await _apiClient.GetPublicHallAsync(hallId, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            if (result.StatusCode == StatusCodes.Status404NotFound)
            {
                Response.StatusCode = StatusCodes.Status404NotFound;
                return View(new HallDetailViewModel { IsNotFound = true });
            }

            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new HallDetailViewModel
            {
                ErrorMessage = "Không thể tải thông tin sảnh. Vui lòng thử lại sau."
            });
        }

        var hall = result.Value;
        var dbImages = hall.ImageUrls
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToList();

        var galleryImages = dbImages.Count > 0
            ? dbImages
            : HallGalleryCatalog.GetImages(hall.HallName, _environment.WebRootPath)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToList();

        return View(new HallDetailViewModel
        {
            HallId = hall.HallId,
            HallCode = hall.HallCode,
            HallName = hall.HallName,
            MinimumCapacity = hall.MinimumCapacity,
            MaximumCapacity = hall.MaximumCapacity,
            RentalPrice = hall.RentalPrice,
            Description = hall.Description,
            ImageUrl = hall.ImageUrl,
            ImageUrls = galleryImages
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            Status = hall.Status,
            StatusName = hall.StatusName
        });
    }
}
