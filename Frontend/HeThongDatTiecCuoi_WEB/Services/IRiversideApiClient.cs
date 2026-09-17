using HeThongDatTiecCuoi_WEB.Models.AdminHall;
using HeThongDatTiecCuoi_WEB.Models.AdminAccount;
using HeThongDatTiecCuoi_WEB.Models.Auth;
namespace HeThongDatTiecCuoi_WEB.Services;
using HeThongDatTiecCuoi_WEB.Models.AdminMenu;
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

    Task<ApiCallResult<MessageResponseDto>> ForgotPasswordAsync(
        ForgotPasswordViewModel model,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MessageResponseDto>> ResetPasswordAsync(
        ResetPasswordViewModel model,
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

    Task<ApiCallResult<AccountActionResponse>> SendPasswordResetLinkAsync(
        int userId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<AccountActionResponse>> UpdateEmployeeStatusAsync(
        int userId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);
    // THỰC ĐƠN
    Task<ApiCallResult<List<MenuViewModel>>> GetMenusAsync(
        string? keyword,
        string? status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MenuViewModel>> CreateMenuAsync(
        CreateMenuForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MenuViewModel>> UpdateMenuAsync(
        int menuId,
        UpdateMenuForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> UpdateMenuStatusAsync(
        int menuId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    // MÓN ĂN
    Task<ApiCallResult<List<DishViewModel>>> GetDishesAsync(
        string? keyword,
        string? category,
        string? status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DishViewModel>> CreateDishAsync(
        CreateDishForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<DishViewModel>> UpdateDishAsync(
        int dishId,
        UpdateDishForm model,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> UpdateDishStatusAsync(
        int dishId,
        string status,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> UploadDishImageAsync(
        int dishId,
        Stream fileStream,
        string fileName,
        string? contentType,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> DeleteDishImageAsync(
        int dishId,
        string accessToken,
        CancellationToken cancellationToken);

    // CHI TIẾT THỰC ĐƠN
    Task<ApiCallResult<List<MenuDishViewModel>>> GetMenuDishesAsync(
        int menuId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiCallResult<MenuDishViewModel>> AddDishToMenuAsync(
        int menuId,
        int dishId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> RemoveDishFromMenuAsync(
        int menuId,
        int dishId,
        string accessToken,
        CancellationToken cancellationToken);

    Task<ApiActionResult> ReorderMenuDishesAsync(
        int menuId,
        List<int> dishIds,
        string accessToken,
        CancellationToken cancellationToken);

}
