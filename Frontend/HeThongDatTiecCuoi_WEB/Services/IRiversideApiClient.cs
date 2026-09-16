using HeThongDatTiecCuoi_WEB.Models.AdminSanh;
using HeThongDatTiecCuoi_WEB.Models.AdminTaiKhoan;
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


    // Sảnh tiệc
    Task<ApiCallResult<List<SanhTiecDto>>> GetDanhSachSanhAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<SanhTiecDto>> GetSanhByIdAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<SanhTiecDto>> CreateSanhAsync(
        SanhTiecDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<SanhTiecDto>> UpdateSanhAsync(
        int id,
        SanhTiecDto model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateTrangThaiSanhAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> DeleteSanhAsync(
        int id,
        string accessToken,
        CancellationToken cancellationToken);


    // Lịch sảnh
    Task<ApiCallResult<LichSanhTuanDto>> GetLichSanhTheoTuanAsync(
        DateTime ngayBatDau,
        int? sanhTiecId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ActionResponseDto>> UpdateTrangThaiLichAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);
    Task<ApiCallResult<List<TaiKhoanDto>>> GetDanhSachTaiKhoanAsync(
    string accessToken,
    string? tuKhoa,
    int? vaiTroId,
    string? trangThai,
    CancellationToken cancellationToken);

    Task<ApiCallResult<List<VaiTroDto>>> GetDanhSachVaiTroAsync(
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> TaoTaiKhoanNhanVienAsync(
        TaoTaiKhoanNhanVienRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> CapNhatTaiKhoanNhanVienAsync(
        int id,
        CapNhatTaiKhoanNhanVienRequest model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> CapNhatTrangThaiTaiKhoanAsync(
        int id,
        string trangThai,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> DatLaiMatKhauAsync(
        int id,
        DatLaiMatKhauRequest model,
        string accessToken,
        CancellationToken cancellationToken);
    Task<ApiCallResult<ThaoTacTaiKhoanResponse>> CapNhatTrangThaiNhanVienAsync(
    int nguoiDungId,
    string trangThai,
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