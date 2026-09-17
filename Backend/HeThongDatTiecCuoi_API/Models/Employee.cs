namespace HeThongDatTiecCuoi_API.Models;

public sealed class Employee
{
    public int EmployeeId { get; set; }
    public int UserId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public int StatusId { get; set; }

    public User User { get; set; } = null!;
    public Status Status { get; set; } = null!;
}
