namespace HeThongDatTiecCuoi_API.DTOs.Payment;

public sealed class CreateDepositPaymentRequestDto
{
    public int BookingId { get; set; }
    public string? BookingCode { get; set; }
    public string? PhoneNumber { get; set; }
}

public sealed class CreateDepositPaymentResponseDto
{
    public long OrderCode { get; set; }
    public string QrCode { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public string? BankBin { get; set; }
    public string? BankName { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal BusinessDeposit { get; set; }
    public int ActualAmount { get; set; }
    public bool IsDemoMode { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public sealed class PaymentStatusResponseDto
{
    public long OrderCode { get; set; }
    public int BookingId { get; set; }
    public string BookingCode { get; set; } = string.Empty;
    public string Status { get; set; } = "PENDING";
    public decimal BusinessDeposit { get; set; }
    public decimal ActualAmount { get; set; }
    public DateTime? PaidAt { get; set; }
    public string Message { get; set; } = string.Empty;
}
