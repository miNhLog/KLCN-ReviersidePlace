using HeThongDatTiecCuoi_API.DTOs.Payment;
using PayOS.Models.Webhooks;

namespace HeThongDatTiecCuoi_API.Services;

public interface IPaymentService
{
    Task<CreateDepositPaymentResponseDto> CreateDepositPaymentAsync(CreateDepositPaymentRequestDto request, int? userId = null);
    Task<CreateContractPayOsLinkResponseDto> CreateContractPaymentAsync(CreateContractPayOsLinkRequestDto request, int? managerUserId = null);
    Task<PaymentStatusResponseDto> GetPaymentStatusAsync(long orderCode);
    Task<PaymentStatusResponseDto> CheckContractPaymentStatusAsync(long orderCode);
    Task<bool> ProcessWebhookAsync(Webhook webhookBody);
    Task<(byte[] PdfBytes, string FileName, InvoiceSignatureResult Signature)> GetSignedInvoicePdfAsync(string bookingCode, string phone);
}