using HeThongDatTiecCuoi_API.Constants.StatusCodes;

namespace HeThongDatTiecCuoi_API.Models;

public sealed class Employee
{
    public int EmployeeId { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Status { get; set; } = EmployeeStatusCodes.Active;

    public User User { get; set; } = null!;
}
