using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Models.Auth;
using HeThongDatTiecCuoi_WEB.Models.Common;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
namespace HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.AdminThucDon;
using Microsoft.AspNetCore.Mvc;

public sealed class RiversideApiClient : IRiversideApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;

    public RiversideApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiCallResult<AuthResponseDto>> LoginAsync(
        LoginViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<AuthResponseDto>(HttpMethod.Post, "api/auth/login", new
        {
            model.DinhDanh,
            model.MatKhau,
            model.LoaiTaiKhoan,
            model.GhiNhoDangNhap
        }, null, cancellationToken);

    public Task<ApiCallResult<AuthResponseDto>> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken) =>
        SendAsync<AuthResponseDto>(HttpMethod.Post, "api/auth/register", new
        {
            model.HoTen,
            model.SoDienThoai,
            model.Email,
            model.MatKhau,
            model.XacNhanMatKhau,
            model.DongYDieuKhoan
        }, null, cancellationToken);

    public Task<ApiCallResult<CurrentUserDto>> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<CurrentUserDto>(HttpMethod.Get, "api/auth/me", null, accessToken, cancellationToken);

    public Task<ApiCallResult<List<HallDto>>> GetHallsAsync(
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<List<HallDto>>(
            HttpMethod.Get,
            "api/halls",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<HallDto>> GetHallByIdAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HallDto>(
            HttpMethod.Get,
            $"api/halls/{hallId}",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<HallDto>> CreateHallAsync(
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HallDto>(
            HttpMethod.Post,
            "api/halls",
            model,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<HallDto>> UpdateHallAsync(
        int hallId,
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<HallDto>(
            HttpMethod.Put,
            $"api/halls/{hallId}",
            model,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<ActionResponseDto>> UpdateHallStatusAsync(
        int hallId,
        string status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return SendAsync<ActionResponseDto>(
            HttpMethod.Patch,
            $"api/halls/{hallId}/status",
            request,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<ActionResponseDto>> DeleteHallAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendAsync<ActionResponseDto>(
            HttpMethod.Delete,
            $"api/halls/{hallId}",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<WeeklyHallScheduleDto>> GetWeeklyHallSchedulesAsync(
        DateTime startDate,
        int? hallId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var url =
            $"api/hall-schedules/weekly?startDate={startDate:yyyy-MM-dd}";

        if (hallId.HasValue)
        {
            url += $"&hallId={hallId.Value}";
        }

        return SendAsync<WeeklyHallScheduleDto>(
            HttpMethod.Get,
            url,
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<ActionResponseDto>> UpdateHallScheduleStatusAsync(
        int hallScheduleId,
        string status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return SendAsync<ActionResponseDto>(
            HttpMethod.Patch,
            $"api/hall-schedules/{hallScheduleId}/status",
            request,
            accessToken,
            cancellationToken);
    }

    // ======================================================
    // ADMIN - QUẢN LÝ TÀI KHOẢN
    // ======================================================

    public Task<ApiCallResult<List<AccountDto>>> GetAccountsAsync(
        string accessToken,
        string? keyword,
        int? roleId,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Add($"keyword={Uri.EscapeDataString(keyword)}");
        }

        if (roleId.HasValue)
        {
            query.Add($"roleId={roleId.Value}");
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query.Add($"status={Uri.EscapeDataString(status)}");
        }

        var uri = "api/admin/accounts";

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return SendAsync<List<AccountDto>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<List<RoleDto>>> GetRolesAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        return SendAsync<List<RoleDto>>(
            HttpMethod.Get,
            "api/admin/accounts/roles",
            null,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        CreateEmployeeAccountAsync(
            CreateEmployeeAccountRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<AccountActionResponse>(
            HttpMethod.Post,
            "api/admin/accounts/employees",
            model,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        UpdateEmployeeAccountAsync(
            int userId,
            UpdateEmployeeAccountRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<AccountActionResponse>(
            HttpMethod.Put,
            $"api/admin/accounts/employees/{userId}",
            model,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        UpdateAccountStatusAsync(
            int userId,
            string status,
            string accessToken,
            CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return SendAsync<AccountActionResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/{userId}/status",
            request,
            accessToken,
            cancellationToken);
    }


    public Task<ApiCallResult<AccountActionResponse>>
        ResetPasswordAsync(
            int userId,
            ResetPasswordRequest model,
            string accessToken,
            CancellationToken cancellationToken)
    {
        return SendAsync<AccountActionResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/{userId}/reset-password",
            model,
            accessToken,
            cancellationToken);
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        HttpMethod method,
        string uri,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, uri);
            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
                return value is null
                    ? ApiCallResult<T>.Failure("API trả về dữ liệu rỗng.")
                    : ApiCallResult<T>.Success(value);
            }

            var error = await response.Content.ReadFromJsonAsync<ApiErrorDto>(JsonOptions, cancellationToken);
            return ApiCallResult<T>.Failure(error?.Message ?? "Yêu cầu không thành công.");
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Failure("Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Failure("Backend API phản hồi quá lâu.");
        }
    }
    public async Task<ApiCallResult<AccountActionResponse>>
    UpdateEmployeeStatusAsync(
        int userId,
        string status,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new StatusRequest
        {
            Status = status
        };

        return await SendAsync<AccountActionResponse>(
            HttpMethod.Patch,
            $"api/admin/accounts/employees/{userId}/status",
            request,
            accessToken,
            cancellationToken
        );
    }

    // =========================================================
    // THỰC ĐƠN
    // =========================================================

    public Task<ApiCallResult<List<ThucDonViewModel>>> GetDanhSachThucDonAsync(
        string? tuKhoa,
        string? trangThai,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            query.Add(
                $"tuKhoa={Uri.EscapeDataString(tuKhoa.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query.Add(
                $"trangThai={Uri.EscapeDataString(trangThai.Trim())}");
        }

        var uri = "api/AdminThucDon";

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return SendJsonAsync<List<ThucDonViewModel>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<ThucDonViewModel>> TaoThucDonAsync(
        TaoThucDonForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<ThucDonViewModel>(
            HttpMethod.Post,
            "api/AdminThucDon",
            new
            {
                model.TenThucDon,
                model.MoTa,
                model.GiaMoiBan
            },
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<ThucDonViewModel>> CapNhatThucDonAsync(
        int id,
        CapNhatThucDonForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<ThucDonViewModel>(
            HttpMethod.Put,
            $"api/AdminThucDon/{id}",
            new
            {
                model.TenThucDon,
                model.MoTa,
                model.GiaMoiBan
            },
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> CapNhatTrangThaiThucDonAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Patch,
            $"api/AdminThucDon/{id}/trang-thai",
            new { TrangThai = trangThai },
            accessToken,
            cancellationToken);


    // =========================================================
    // MÓN ĂN
    // =========================================================

    public Task<ApiCallResult<List<MonAnViewModel>>> GetDanhSachMonAnAsync(
        string? tuKhoa,
        string? nhomMon,
        string? trangThai,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var query = new List<string>();

        if (!string.IsNullOrWhiteSpace(tuKhoa))
        {
            query.Add(
                $"tuKhoa={Uri.EscapeDataString(tuKhoa.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(nhomMon))
        {
            query.Add(
                $"nhomMon={Uri.EscapeDataString(nhomMon.Trim())}");
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query.Add(
                $"trangThai={Uri.EscapeDataString(trangThai.Trim())}");
        }

        var uri = "api/AdminMonAn";

        if (query.Count > 0)
        {
            uri += "?" + string.Join("&", query);
        }

        return SendJsonAsync<List<MonAnViewModel>>(
            HttpMethod.Get,
            uri,
            null,
            accessToken,
            cancellationToken);
    }

    public Task<ApiCallResult<MonAnViewModel>> TaoMonAnAsync(
        TaoMonAnForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<MonAnViewModel>(
            HttpMethod.Post,
            "api/AdminMonAn",
            new
            {
                model.TenMon,
                model.NhomMon
            },
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<MonAnViewModel>> CapNhatMonAnAsync(
        int id,
        CapNhatMonAnForm model,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<MonAnViewModel>(
            HttpMethod.Put,
            $"api/AdminMonAn/{id}",
            new
            {
                model.TenMon,
                model.NhomMon
            },
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> CapNhatTrangThaiMonAnAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Patch,
            $"api/AdminMonAn/{id}/trang-thai",
            new { TrangThai = trangThai },
            accessToken,
            cancellationToken);

    public async Task<ApiActionResult> UploadHinhAnhMonAnAsync(
        int id,
        Stream fileStream,
        string fileName,
        string? contentType,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"api/AdminMonAn/{id}/hinh-anh");

            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            using var multipart = new MultipartFormDataContent();

            using var fileContent = new StreamContent(fileStream);

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                fileContent.Headers.ContentType =
                    MediaTypeHeaderValue.Parse(contentType);
            }

            multipart.Add(fileContent, "file", fileName);

            request.Content = multipart;

            using var response =
                await _httpClient.SendAsync(request, cancellationToken);

            return response.IsSuccessStatusCode
                ? ApiActionResult.Success()
                : ApiActionResult.Failure(
                    await ReadErrorAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiActionResult.Failure(
                "Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiActionResult.Failure(
                "Backend API phản hồi quá lâu.");
        }
    }

    public Task<ApiActionResult> XoaHinhAnhMonAnAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Delete,
            $"api/AdminMonAn/{id}/hinh-anh",
            null,
            accessToken,
            cancellationToken);


    // =========================================================
    // CHI TIẾT THỰC ĐƠN
    // =========================================================

    public Task<ApiCallResult<List<ChiTietThucDonViewModel>>> GetMonAnTrongThucDonAsync(
        int thucDonId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<List<ChiTietThucDonViewModel>>(
            HttpMethod.Get,
            $"api/AdminThucDon/{thucDonId}/mon-an",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiCallResult<ChiTietThucDonViewModel>> ThemMonVaoThucDonAsync(
        int thucDonId,
        int monAnId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendJsonAsync<ChiTietThucDonViewModel>(
            HttpMethod.Post,
            $"api/AdminThucDon/{thucDonId}/mon-an",
            new { MonAnID = monAnId },
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> XoaMonKhoiThucDonAsync(
        int thucDonId,
        int monAnId,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Delete,
            $"api/AdminThucDon/{thucDonId}/mon-an/{monAnId}",
            null,
            accessToken,
            cancellationToken);

    public Task<ApiActionResult> SapXepMonAnAsync(
        int thucDonId,
        List<int> danhSachMonAnID,
        string accessToken,
        CancellationToken cancellationToken) =>
        SendActionJsonAsync(
            HttpMethod.Put,
            $"api/AdminThucDon/{thucDonId}/mon-an/sap-xep",
            new { DanhSachMonAnID = danhSachMonAnID },
            accessToken,
            cancellationToken);


    // =========================================================
    // HTTP HELPERS
    // =========================================================

    private async Task<ApiCallResult<T>> SendJsonAsync<T>(
        HttpMethod method,
        string uri,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request =
                new HttpRequestMessage(method, uri);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue(
                        "Bearer",
                        accessToken);
            }

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var value =
                    await response.Content.ReadFromJsonAsync<T>(
                        JsonOptions,
                        cancellationToken);

                return value is null
                    ? ApiCallResult<T>.Failure(
                        "API trả về dữ liệu rỗng.")
                    : ApiCallResult<T>.Success(value);
            }

            return ApiCallResult<T>.Failure(
                await ReadErrorAsync(
                    response,
                    cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiCallResult<T>.Failure(
                "Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiCallResult<T>.Failure(
                "Backend API phản hồi quá lâu.");
        }
    }

    private async Task<ApiActionResult> SendActionJsonAsync(
        HttpMethod method,
        string uri,
        object? body,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request =
                new HttpRequestMessage(method, uri);

            if (body is not null)
            {
                request.Content = JsonContent.Create(body);
            }

            request.Headers.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    accessToken);

            using var response =
                await _httpClient.SendAsync(
                    request,
                    cancellationToken);

            return response.IsSuccessStatusCode
                ? ApiActionResult.Success()
                : ApiActionResult.Failure(
                    await ReadErrorAsync(
                        response,
                        cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiActionResult.Failure(
                "Không thể kết nối Backend API. Hãy kiểm tra API đã chạy hay chưa.");
        }
        catch (TaskCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return ApiActionResult.Failure(
                "Backend API phản hồi quá lâu.");
        }
    }

    private static async Task<string> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var error =
                await response.Content.ReadFromJsonAsync<ApiErrorDto>(
                    JsonOptions,
                    cancellationToken);

            if (!string.IsNullOrWhiteSpace(error?.Message))
            {
                return error.Message;
            }
        }
        catch
        {
            // Nếu Backend trả về body không phải JSON thì đọc text bên dưới.
        }

        var text =
            await response.Content.ReadAsStringAsync(
                cancellationToken);

        return string.IsNullOrWhiteSpace(text)
            ? $"Yêu cầu không thành công ({(int)response.StatusCode})."
            : text;
    }

}
