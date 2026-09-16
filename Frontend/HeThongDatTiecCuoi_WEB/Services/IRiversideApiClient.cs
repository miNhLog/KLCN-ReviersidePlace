using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Models.Auth;
namespace HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.AdminThucDon;
using Microsoft.AspNetCore.Mvc;

public interface IRiversideApiClient
{
    // Auth
    Task<ApiCallResult<AuthResponseDto>> LoginAsync(
        LoginViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AuthResponseDto>> RegisterAsync(
        RegisterViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<CurrentUserDto>> GetCurrentUserAsync(
        string accessToken,
        CancellationToken cancellationToken);


    // Hall management
    Task<ApiCallResult<List<HallDto>>> GetHallsAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<HallDto>> GetHallByIdAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<HallDto>> CreateHallAsync(
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<HallDto>> UpdateHallAsync(
        int hallId,
        HallDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateHallStatusAsync(
        int hallId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> DeleteHallAsync(
        int hallId,
        string accessToken,
        CancellationToken cancellationToken);


    // Hall schedules
    Task<ApiCallResult<WeeklyHallScheduleDto>> GetWeeklyHallSchedulesAsync(
        DateTime startDate,
        int? hallId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateHallScheduleStatusAsync(
        int hallScheduleId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);
    Task<ApiCallResult<List<AccountDto>>> GetAccountsAsync(
        string accessToken,
        string? keyword,
        int? roleId,
        string? status,
        CancellationToken cancellationToken);

    Task<ApiCallResult<List<RoleDto>>> GetRolesAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> CreateEmployeeAccountAsync(
        CreateEmployeeAccountRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> UpdateEmployeeAccountAsync(
        int userId,
        UpdateEmployeeAccountRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> UpdateAccountStatusAsync(
        int userId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> ResetPasswordAsync(
        int userId,
        ResetPasswordRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> UpdateEmployeeStatusAsync(
        int userId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);
    // THỰC ĐƠN
    Task<ApiCallResult<List<ThucDonViewModel>>> GetDanhSachThucDonAsync(
        string? tuKhoa,
        string? trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThucDonViewModel>> TaoThucDonAsync(
        TaoThucDonForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThucDonViewModel>> CapNhatThucDonAsync(
        int id,
        CapNhatThucDonForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> CapNhatTrangThaiThucDonAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    // MÓN ĂN
    Task<ApiCallResult<List<MonAnViewModel>>> GetDanhSachMonAnAsync(
        string? tuKhoa,
        string? nhomMon,
        string? trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MonAnViewModel>> TaoMonAnAsync(
        TaoMonAnForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MonAnViewModel>> CapNhatMonAnAsync(
        int id,
        CapNhatMonAnForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> CapNhatTrangThaiMonAnAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> UploadHinhAnhMonAnAsync(
        int id,
        Stream fileStream,
        string fileName,
        string? contentType,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> XoaHinhAnhMonAnAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken);

    // CHI TIẾT THỰC ĐƠN
    Task<ApiCallResult<List<ChiTietThucDonViewModel>>> GetMonAnTrongThucDonAsync(
        int thucDonId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ChiTietThucDonViewModel>> ThemMonVaoThucDonAsync(
        int thucDonId,
        int monAnId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> XoaMonKhoiThucDonAsync(
        int thucDonId,
        int monAnId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> SapXepMonAnAsync(
        int thucDonId,
        List<int> danhSachMonAnID,
        string accessToken,
        CancellationToken cancellationToken);

}
