using HeThongDatTiecCuoi_API.DTOs.Payment;
using PayOS.Models.Webhooks;

namespace HeThongDatTiecCuoi_API.Services;

public interface IPaymentService
{
    Task<CreateDepositPaymentResponseDto> CreateDepositPaymentAsync(CreateDepositPaymentRequestDto request, int? userId = null);
    Task<PaymentStatusResponseDto> GetPaymentStatusAsync(long orderCode);
    Task<bool> ProcessWebhookAsync(Webhook webhookBody);
}
