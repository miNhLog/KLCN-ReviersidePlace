namespace HeThongDatTiecCuoi_API.Models;

public sealed class Customer
{
    public int CustomerId { get; set; }
    public int? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public User? User { get; set; }
}
