using System.Text.Json;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Payment;
using HeThongDatTiecCuoi_API.Models;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;
using PaymentTransactionEntity = HeThongDatTiecCuoi_API.Models.PaymentTransaction;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace HeThongDatTiecCuoi_API.Services;

public sealed class PayOSPaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly PayOSClient _payOSClient;
    private readonly IOptions<AppPayOSOptions> _options;
    private readonly IStatusService _statusService;
    private readonly ILogger<PayOSPaymentService> _logger;

    public PayOSPaymentService(
        ApplicationDbContext context,
        PayOSClient payOSClient,
        IOptions<AppPayOSOptions> options,
        IStatusService statusService,
        ILogger<PayOSPaymentService> logger)
    {
        _context = context;
        _payOSClient = payOSClient;
        _options = options;
        _statusService = statusService;
        _logger = logger;
    }

    public async Task<CreateDepositPaymentResponseDto> CreateDepositPaymentAsync(
        CreateDepositPaymentRequestDto request,
        int? userId = null)
    {
        if (request.BookingId <= 0)
            throw new ArgumentException("Mã đặt tiệc ID không hợp lệ.", nameof(request.BookingId));

        var query = _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.Status)
            .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .AsQueryable();

        var booking = await query.SingleOrDefaultAsync(b => b.BookingId == request.BookingId);
        if (booking == null)
            throw new KeyNotFoundException("Không tìm thấy đơn đặt tiệc cưới này.");

        // Xác thực quyền sở hữu: nếu đăng nhập thì kiểm tra UserId, nếu là khách tra cứu thì kiểm tra mã + SĐT
        if (userId.HasValue && userId.Value > 0)
        {
            if (booking.Customer.UserId != userId.Value)
            {
                // Cho phép tra cứu nếu khớp bookingCode + phone
                if (!IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
                    throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thanh toán cho đơn tiệc này.");
            }
        }
        else
        {
            if (!IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
                throw new UnauthorizedAccessException("Thông tin mã đặt tiệc hoặc số điện thoại không khớp với hồ sơ.");
        }

        // Kiểm tra trạng thái đơn: phải ở trạng thái PENDING và chưa thanh toán cọc
        if (booking.PaidAmount > 0)
            throw new InvalidOperationException("Đơn đặt tiệc này đã được thanh toán cọc trước đó.");

        if (booking.Status.StatusCode != BookingStatusCodes.Pending)
            throw new InvalidOperationException($"Chỉ có thể đặt cọc khi tiệc ở trạng thái Chờ xác nhận (Hiện tại: {booking.Status.StatusName}).");

        // 1. Tính tiền cọc nghiệp vụ (10% tổng dự toán)
        decimal totalEstimate = booking.EstimatedTotal ?? 0m;
        if (totalEstimate <= 0)
            throw new InvalidOperationException("Tổng dự toán tiệc không hợp lệ để tính tiền cọc.");

        decimal businessDeposit = Math.Round(totalEstimate * 0.10m, 0);

        // 2. Tính số tiền thực tế quét VietQR theo tỷ lệ Demo Scale
        int actualAmount;
        if (_options.Value.IsDemoMode)
        {
            int ratio = _options.Value.DemoScaleRatio > 0 ? _options.Value.DemoScaleRatio : 10000;
            actualAmount = Math.Max(2000, (int)Math.Round(businessDeposit / ratio, MidpointRounding.AwayFromZero));
        }
        else
        {
            actualAmount = (int)businessDeposit;
        }

        // 3. Sinh OrderCode dương duy nhất chuẩn PayOS (< 9007199254740991)
        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // 4. Chuẩn hóa mô tả chuyển khoản (PayOS yêu cầu <= 25 ký tự, không dấu)
        string description = $"RSP {booking.BookingId}";
        if (description.Length > 25)
            description = description.Substring(0, 25);

        // 5. Chuẩn bị CreatePaymentLinkRequest cho PayOS SDK 2.1.0
        var items = new List<PaymentLinkItem>
        {
            new PaymentLinkItem
            {
                Name = $"Coc tiec {booking.BookingCode}",
                Quantity = 1,
                Price = actualAmount
            }
        };

        var paymentRequest = new CreatePaymentLinkRequest
        {
            OrderCode = orderCode,
            Amount = actualAmount,
            Description = description,
            Items = items,
            CancelUrl = _options.Value.CancelUrl,
            ReturnUrl = _options.Value.ReturnUrl
        };

        _logger.LogInformation(
            "Tạo link thanh toán PayOS: BookingId={BookingId}, OrderCode={OrderCode}, BusinessDeposit={BusinessDeposit}, ActualAmount={ActualAmount}, IsDemo={IsDemo}",
            booking.BookingId, orderCode, businessDeposit, actualAmount, _options.Value.IsDemoMode);

        CreatePaymentLinkResponse paymentResult;
        try
        {
            paymentResult = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi PayOS SDK PaymentRequests.CreateAsync cho BookingId={BookingId}", booking.BookingId);
            throw new InvalidOperationException($"Không thể tạo liên kết thanh toán VietQR lúc này: {ex.Message}", ex);
        }

        // 6. Lưu bản ghi GiaoDichThanhToan trạng thái PENDING
        var transaction = new PaymentTransactionEntity
        {
            DatTiecId = booking.BookingId,
            OrderCode = orderCode,
            SoTienGiaoDich = actualAmount,
            SoTienGhiNhan = businessDeposit,
            PhuongThuc = "VIETQR_PAYOS",
            TrangThai = "PENDING",
            NgayTao = DateTime.Now
        };

        _context.PaymentTransactions.Add(transaction);
        booking.DepositAmount = businessDeposit;
        await _context.SaveChangesAsync();

        return new CreateDepositPaymentResponseDto
        {
            OrderCode = orderCode,
            QrCode = paymentResult.QrCode ?? string.Empty,
            CheckoutUrl = paymentResult.CheckoutUrl ?? string.Empty,
            AccountNumber = paymentResult.AccountNumber,
            AccountName = paymentResult.AccountName,
            BankBin = paymentResult.Bin,
            BankName = paymentResult.Bin,
            Description = description,
            BusinessDeposit = businessDeposit,
            ActualAmount = actualAmount,
            IsDemoMode = _options.Value.IsDemoMode,
            ExpiresAt = DateTime.Now.AddMinutes(10)
        };
    }

    public async Task<bool> ProcessWebhookAsync(Webhook webhookBody)
    {
        if (webhookBody == null)
            throw new ArgumentNullException(nameof(webhookBody));

        // 1. Xác thực chữ ký số webhook SHA256 bằng PayOSClient
        WebhookData webhookData;
        try
        {
            webhookData = await _payOSClient.Webhooks.VerifyAsync(webhookBody);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xác thực chữ ký Webhook PayOS thất bại. Dữ liệu có thể bị giả mạo!");
            throw new UnauthorizedAccessException("Chữ ký Webhook không hợp lệ.", ex);
        }

        _logger.LogInformation(
            "Nhận Webhook PayOS hợp lệ: OrderCode={OrderCode}, Amount={Amount}, Code={Code}, Desc={Desc}",
            webhookData.OrderCode, webhookData.Amount, webhookData.Code, webhookData.Description);

        // 2. Tìm bản ghi GiaoDichThanhToan theo OrderCode
        var tx = await _context.PaymentTransactions
            .Include(t => t.Booking)
            .SingleOrDefaultAsync(t => t.OrderCode == webhookData.OrderCode);

        if (tx == null)
        {
            _logger.LogWarning("Không tìm thấy GiaoDichThanhToan với OrderCode={OrderCode} trong hệ thống.", webhookData.OrderCode);
            return true; // Trả về true để PayOS không retry vô tận
        }

        // 3. Cơ chế Idempotency: nếu đã PAID thì return thành công ngay, tránh ghi nhận tiền 2 lần
        if (tx.TrangThai == "PAID")
        {
            _logger.LogInformation("Giao dịch OrderCode={OrderCode} đã được xử lý PAID trước đó. Bỏ qua ghi nhận trùng lặp.", webhookData.OrderCode);
            return true;
        }

        // 4. Kiểm tra mã phản hồi thành công từ PayOS (code == "00")
        if (webhookData.Code == "00" || webhookBody.Code == "00")
        {
            await CompletePaymentTransactionAsync(tx, webhookData.OrderCode, JsonSerializer.Serialize(webhookBody));
            return true;
        }

        // Nếu mã trạng thái khác (hủy/thất bại)
        tx.TrangThai = "FAILED";
        tx.PayloadWebhook = JsonSerializer.Serialize(webhookBody);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<PaymentStatusResponseDto> GetPaymentStatusAsync(long orderCode)
    {
        var tx = await _context.PaymentTransactions
            .Include(t => t.Booking)
            .SingleOrDefaultAsync(t => t.OrderCode == orderCode);

        if (tx == null)
        {
            return new PaymentStatusResponseDto
            {
                OrderCode = orderCode,
                Status = "NOT_FOUND",
                Message = "Không tìm thấy thông tin giao dịch."
            };
        }

        // Fallback kiểm tra trực tiếp PayOS nếu vẫn PENDING sau một khoảng thời gian
        // (Đặc biệt hữu ích khi chạy Localhost chưa bật Webhook qua Ngrok)
        if (tx.TrangThai == "PENDING")
        {
            try
            {
                PaymentLink linkInfo = await _payOSClient.PaymentRequests.GetAsync(orderCode);
                var statusStr = linkInfo?.Status.ToString();
                if (linkInfo != null && string.Equals(statusStr, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Fallback Sync: PayOS báo PAID cho OrderCode={OrderCode}, tự động hoàn tất giao dịch.", orderCode);
                    await CompletePaymentTransactionAsync(tx, orderCode, "Auto-polled from PayOS PaymentRequests.GetAsync");
                }
                else if (linkInfo != null && string.Equals(statusStr, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    tx.TrangThai = "CANCELLED";
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Kiểm tra PayOS fallback cho OrderCode={OrderCode} chưa có kết quả mới.", orderCode);
            }
        }

        return new PaymentStatusResponseDto
        {
            OrderCode = tx.OrderCode,
            BookingId = tx.DatTiecId,
            BookingCode = tx.Booking.BookingCode,
            Status = tx.TrangThai,
            BusinessDeposit = tx.SoTienGhiNhan,
            ActualAmount = tx.SoTienGiaoDich,
            PaidAt = tx.NgayThanhToan,
            Message = tx.TrangThai == "PAID"
                ? "Thanh toán đặt cọc thành công!"
                : (tx.TrangThai == "CANCELLED" ? "Giao dịch đã bị hủy." : "Đang chờ thanh toán...")
        };
    }

    private async Task CompletePaymentTransactionAsync(PaymentTransactionEntity tx, long orderCode, string payload)
    {
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            tx.TrangThai = "PAID";
            tx.NgayThanhToan = DateTime.Now;
            tx.PayloadWebhook = payload;

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.HallSchedule)
                .Include(b => b.Contract)
                .SingleAsync(b => b.BookingId == tx.DatTiecId);

            // Cập nhật DatTiec: Trạng thái -> CONFIRMED, Tiền cọc & Số tiền đã thanh toán ghi nhận đủ cọc thật
            var confirmedStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Booking, BookingStatusCodes.Confirmed);
            booking.StatusId = confirmedStatusId;
            booking.DepositAmount = tx.SoTienGhiNhan;
            booking.PaidAmount = booking.PaidAmount + tx.SoTienGhiNhan;

            // Khóa chính thức lịch sảnh: LichSanh -> BOOKED (TrangThaiID = 302)
            if (booking.HallSchedule != null)
            {
                var bookedScheduleId = await _statusService.GetStatusIdAsync(StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked);
                booking.HallSchedule.StatusId = bookedScheduleId;
            }

            // Đồng bộ hợp đồng (HopDong) và biên lai thanh toán (ThanhToan) để hiển thị trong Manager/Admin
            if (booking.Contract == null)
            {
                var draftContractStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Contract, ContractStatusCodes.Draft);
                var contract = new Contract
                {
                    BookingId = booking.BookingId,
                    ContractCode = $"HD-{booking.BookingCode}",
                    CreatedDate = DateTime.Today,
                    TotalValue = booking.EstimatedTotal ?? tx.SoTienGhiNhan,
                    PaymentTerms = "Đặt cọc 10% trực tuyến qua VietQR PayOS",
                    StatusId = draftContractStatusId
                };
                _context.Contracts.Add(contract);
                await _context.SaveChangesAsync();
                booking.Contract = contract;
            }

            var paymentSuccessStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Success);
            var payment = new Payment
            {
                ContractId = booking.Contract.ContractId,
                PaymentType = PaymentTypes.Deposit,
                Amount = tx.SoTienGhiNhan,
                PaymentDate = DateTime.Now,
                PaymentMethod = "VIETQR_PAYOS",
                TransactionCode = orderCode.ToString(),
                StatusId = paymentSuccessStatusId
            };
            _context.Payments.Add(payment);

            // Ghi nhật ký hệ thống (AuditLog)
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "PAYMENT",
                EntityName = "DatTiec",
                EntityId = booking.BookingId,
                OldData = $"BookingCode={booking.BookingCode};TrangThai=PENDING;PaidAmount=0",
                NewData = $"BookingCode={booking.BookingCode};TrangThai=CONFIRMED;PaidAmount={booking.PaidAmount};OrderCode={orderCode}",
                Timestamp = DateTime.Now,
                Notes = $"Khách hàng đã thanh toán cọc thành công {tx.SoTienGhiNhan:N0} đ qua VietQR PayOS. Lịch sảnh đã được khóa chính thức."
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation(
                "Đã hoàn tất thanh toán cọc thành công: BookingCode={BookingCode}, OrderCode={OrderCode}, Số tiền ghi nhận={Amount:N0} VNĐ",
                booking.BookingCode, orderCode, tx.SoTienGhiNhan);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi cập nhật CSDL sau khi thanh toán cọc OrderCode={OrderCode}", orderCode);
            throw;
        }
    }

    private static bool IsGuestVerified(WeddingBooking booking, string? inputCode, string? inputPhone)
    {
        if (string.IsNullOrWhiteSpace(inputCode) || string.IsNullOrWhiteSpace(inputPhone))
            return false;

        var cleanCode = inputCode.Trim().ToUpperInvariant();
        var cleanPhone = inputPhone.Trim();

        return booking.BookingCode.Equals(cleanCode, StringComparison.OrdinalIgnoreCase) &&
               booking.Customer.PhoneNumber.Equals(cleanPhone, StringComparison.OrdinalIgnoreCase);
    }
}
