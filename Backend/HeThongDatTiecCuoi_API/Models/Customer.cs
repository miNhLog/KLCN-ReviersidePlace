using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace HeThongDatTiecCuoi_API.Models;

public sealed class Customer
{
    public int CustomerId { get; set; }
    public int? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;

    public User? User { get; set; }

    [NotMapped]
    [JsonIgnore]
    public string HoTen => FullName;
}
