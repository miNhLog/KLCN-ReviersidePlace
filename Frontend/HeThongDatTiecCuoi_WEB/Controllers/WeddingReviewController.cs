using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeThongDatTiecCuoi_WEB.Controllers;

[Route("danh-gia")]
public sealed class WeddingReviewController(IHttpClientFactory clients) : Controller
{
    [AllowAnonymous]
    [HttpGet("khach-moi/{token}")]
    public Task<IActionResult> Guest(string token, CancellationToken ct) => ShowForm($"api/reviews/guest/{Uri.EscapeDataString(token)}", token, false, ct);

    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [HttpPost("khach-moi/{token}")]
    public Task<IActionResult> GuestSubmit(string token, ReviewInput input, CancellationToken ct) => Submit($"api/reviews/guest/{Uri.EscapeDataString(token)}", token, false, input, ct);

    [Authorize(Roles = "Khách hàng")]
    [HttpGet("chu-tiec/{bookingCode}")]
    public Task<IActionResult> Host(string bookingCode, CancellationToken ct) => ShowForm($"api/reviews/host/{Uri.EscapeDataString(bookingCode)}", bookingCode, true, ct);

    [Authorize(Roles = "Khách hàng")]
    [ValidateAntiForgeryToken]
    [HttpPost("chu-tiec/{bookingCode}")]
    public Task<IActionResult> HostSubmit(string bookingCode, ReviewInput input, CancellationToken ct) => Submit($"api/reviews/host/{Uri.EscapeDataString(bookingCode)}", bookingCode, true, input, ct);

    [Authorize(Roles = "Quản trị viên,Quản lý,Quản lý sảnh")]
    [HttpGet("quan-ly")]
    public async Task<IActionResult> Management(string? search, int? hallId, DateTime? fromDate,
        DateTime? toDate, string? qrState, string? reviewerType, int? bookingId, int page, CancellationToken ct)
    {
        var query = new List<string>();
        void Add(string name, string? value) { if (!string.IsNullOrWhiteSpace(value)) query.Add($"{name}={Uri.EscapeDataString(value)}"); }
        Add("search", search); Add("hallId", hallId?.ToString()); Add("fromDate", fromDate?.ToString("yyyy-MM-dd"));
        Add("toDate", toDate?.ToString("yyyy-MM-dd")); Add("qrState", qrState); Add("reviewerType", reviewerType);
        Add("page", Math.Max(1, page).ToString()); Add("pageSize", "20");
        var uri = "api/reviews/statistics/overview" + (query.Count == 0 ? "" : "?" + string.Join('&', query));
        using var response = await Send(HttpMethod.Get, uri, null, true, ct);
        if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode);
        ViewBag.Overview = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        ViewBag.SelectedBookingId = bookingId;
        return View("Management");
    }

    [Authorize(Roles = "Quản trị viên,Quản lý,Quản lý sảnh")]
    [HttpGet("quan-ly/{bookingId:int}")]
    public async Task<IActionResult> Detail(int bookingId, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, $"api/reviews/statistics/bookings/{bookingId}", null, true, ct);
        if (!response.IsSuccessStatusCode) return View("Unavailable", model: await Error(response, ct));
        ViewBag.Statistics = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        return View("Detail");
    }

    [Authorize(Roles = "Quản trị viên,Quản lý,Quản lý sảnh")]
    [HttpGet("quan-ly/{bookingId:int}/du-lieu")]
    public async Task<IActionResult> DetailData(int bookingId, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, $"api/reviews/statistics/bookings/{bookingId}", null, true, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return new ContentResult { StatusCode = (int)response.StatusCode, ContentType = "application/json; charset=utf-8", Content = body };
    }

    [Authorize(Roles = "Quản trị viên,Quản lý,Quản lý sảnh")]
    [HttpGet("quan-ly/{bookingId:int}/qr.png")]
    public async Task<IActionResult> Qr(int bookingId, [FromQuery] bool download, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, $"api/reviews/management/{bookingId}/qr.png", null, true, ct);
        if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return download ? File(bytes, "image/png", $"QR-danh-gia-{bookingId}.png") : File(bytes, "image/png");
    }

    private async Task<IActionResult> ShowForm(string uri, string key, bool authenticated, CancellationToken ct)
    {
        using var response = await Send(HttpMethod.Get, uri, null, authenticated, ct);
        if (!response.IsSuccessStatusCode) return View("Unavailable", model: await Error(response, ct));
        ViewBag.Form = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct)).RootElement.Clone();
        ViewBag.Key = key; ViewBag.IsHost = authenticated;
        return View("Form", new ReviewInput { SubmissionId = Guid.NewGuid().ToString("N") });
    }

    private async Task<IActionResult> Submit(string uri, string key, bool authenticated, ReviewInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await ShowForm(uri, key, authenticated, ct);
        using var response = await Send(HttpMethod.Post, uri, input, authenticated, ct);
        if (response.IsSuccessStatusCode) return View("Thanks");
        ViewBag.SubmitError = await Error(response, ct);
        return await ShowForm(uri, key, authenticated, ct);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string uri, object? body, bool authenticated, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null) request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        if (authenticated && Request.Cookies.TryGetValue("rp_api_token", out var token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await clients.CreateClient("RiversideApi").SendAsync(request, ct);
    }

    private static async Task<string> Error(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            return json.RootElement.TryGetProperty("message", out var message) ? message.GetString() ?? "Không thể thực hiện." : "Không thể thực hiện.";
        }
        catch { return "Không thể thực hiện yêu cầu lúc này."; }
    }
}

public sealed class ReviewInput
{
    public int? HallScore { get; set; }
    public int? FoodScore { get; set; }
    public int? ServiceScore { get; set; }
    public int? SoundScore { get; set; }
    public int? LightingScore { get; set; }
    [System.ComponentModel.DataAnnotations.Range(1, 5)] public int OverallScore { get; set; }
    [System.ComponentModel.DataAnnotations.StringLength(1000)] public string? Comment { get; set; }
    public string? SubmissionId { get; set; }
}
