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
    private readonly IEmailService _emailService;
    private readonly IInvoicePdfGenerator _invoicePdfGenerator;
    private readonly IInvoiceDigitalSignatureService _signatureService;
    private readonly ILogger<PayOSPaymentService> _logger;
    private readonly IScheduleChangeService _scheduleChanges;

    public PayOSPaymentService(
        ApplicationDbContext context,
        PayOSClient payOSClient,
        IOptions<AppPayOSOptions> options,
        IStatusService statusService,
        IEmailService emailService,
        IInvoicePdfGenerator invoicePdfGenerator,
        IInvoiceDigitalSignatureService signatureService,
        IScheduleChangeService scheduleChanges,
        ILogger<PayOSPaymentService> logger)
    {
        _context = context;
        _payOSClient = payOSClient;
        _options = options;
        _statusService = statusService;
        _emailService = emailService;
        _invoicePdfGenerator = invoicePdfGenerator;
        _signatureService = signatureService;
        _scheduleChanges = scheduleChanges;
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
            TransactionType = PaymentTransactionTypes.Deposit,
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

        if (tx.TransactionType == PaymentTransactionTypes.ScheduleChange)
        {
            return await _scheduleChanges.ProcessVerifiedPaymentAsync(
                webhookData.OrderCode, webhookData.Amount, webhookData.Code ?? webhookBody.Code,
                JsonSerializer.Serialize(webhookBody), CancellationToken.None);
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
            if (tx.TransactionType == PaymentTransactionTypes.Installment || tx.TransactionType == PaymentTransactionTypes.Settlement)
            {
                await CompleteContractPaymentTransactionAsync(tx, webhookData.OrderCode, JsonSerializer.Serialize(webhookBody));
                return true;
            }

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
                    if (tx.TransactionType == PaymentTransactionTypes.Installment || tx.TransactionType == PaymentTransactionTypes.Settlement)
                    {
                        await CompleteContractPaymentTransactionAsync(tx, orderCode, "Auto-polled from PayOS PaymentRequests.GetAsync");
                    }
                    else
                    {
                        await CompletePaymentTransactionAsync(tx, orderCode, "Auto-polled from PayOS PaymentRequests.GetAsync");
                    }
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
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .Include(b => b.Menu)
                .Include(b => b.DecorationPackage)
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

            // Gửi email xác nhận đặt cọc thành công kèm Hóa đơn / Biên lai điện tử có chữ ký số (PAdES)
            if (!string.IsNullOrWhiteSpace(booking.Customer.Email))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var remaining = Math.Max(0, (booking.EstimatedTotal ?? tx.SoTienGhiNhan) - booking.PaidAmount);
                        var invoiceDto = new DTOs.Invoice.InvoicePdfDto
                        {
                            InvoiceNumber = $"HDDT-{booking.BookingCode}-01",
                            BookingCode = booking.BookingCode,
                            CustomerName = booking.Customer.FullName,
                            CustomerPhone = booking.Customer.PhoneNumber,
                            CustomerEmail = booking.Customer.Email ?? string.Empty,
                            PaymentType = "Đặt cọc (10%)",
                            PaymentMethod = "VIETQR_PAYOS",
                            TransactionCode = orderCode.ToString(),
                            InvoiceDate = DateTime.Now,
                            EventDate = booking.HallSchedule?.Date ?? DateTime.Today,
                            Shift = booking.HallSchedule?.Shift ?? "Tối",
                            HallName = booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                            GuestCount = booking.GuestCount,
                            TotalTables = (booking.GuestCount + 9) / 10,
                            MenuName = booking.Menu?.MenuName,
                            DecorName = booking.DecorationPackage?.TenGoi,
                            TotalEstimatedCost = booking.EstimatedTotal ?? tx.SoTienGhiNhan,
                            AmountPaid = tx.SoTienGhiNhan,
                            AccumulatedPaid = booking.PaidAmount,
                            RemainingDebt = remaining,
                            VerificationUrl = $"https://localhost:7102/MyBooking?bookingCode={Uri.EscapeDataString(booking.BookingCode)}&phone={Uri.EscapeDataString(booking.Customer.PhoneNumber)}",
                            IsDigitallySigned = true,
                            SignedAt = DateTime.Now
                        };

                        bool isFullSettlement = remaining <= 0 || booking.PaidAmount >= (booking.EstimatedTotal ?? tx.SoTienGhiNhan);
                        if (isFullSettlement)
                        {
                            invoiceDto.PaymentType = "Thanh toán toàn bộ (100%)";
                        }

                        byte[] unsignedPdf = _invoicePdfGenerator.Generate(invoiceDto);
                        var signResult = _signatureService.SignPdfDocument(unsignedPdf, invoiceDto.InvoiceNumber);

                        if (isFullSettlement)
                        {
                            await _emailService.SendFullPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                booking.Contract?.ContractCode ?? $"HD-{booking.BookingCode}",
                                booking.PaidAmount,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"HoaDonDienTu_{booking.BookingCode}.pdf");
                        }
                        else
                        {
                            await _emailService.SendDepositPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                orderCode,
                                tx.SoTienGhiNhan,
                                remaining,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"BienLaiDatCoc_{booking.BookingCode}.pdf");
                        }
                    }
                    catch (Exception mailEx)
                    {
                        _logger.LogWarning(mailEx, "Không thể gửi email xác nhận đặt cọc thành công cho {Email}", booking.Customer.Email);
                    }
                });
            }
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

    public async Task<(byte[] PdfBytes, string FileName, InvoiceSignatureResult Signature)> GetSignedInvoicePdfAsync(string bookingCode, string phone)
    {
        if (string.IsNullOrWhiteSpace(bookingCode) || string.IsNullOrWhiteSpace(phone))
            throw new ArgumentException("Mã đặt tiệc và số điện thoại không được để trống.");

        var cleanCode = bookingCode.Trim().ToUpperInvariant();
        var cleanPhone = phone.Trim();

        var booking = await _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.Status)
            .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
            .Include(b => b.Menu)
            .Include(b => b.DecorationPackage)
            .Include(b => b.Contract).ThenInclude(c => c!.Payments)
            .Include(b => b.PaymentTransactions)
            .SingleOrDefaultAsync(b => b.BookingCode == cleanCode);

        if (booking == null)
            throw new KeyNotFoundException($"Không tìm thấy đơn đặt tiệc với mã {cleanCode}.");

        if (!booking.Customer.PhoneNumber.Equals(cleanPhone, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Số điện thoại không khớp với thông tin đơn đặt tiệc.");

        var latestPayment = booking.Contract?.Payments?
            .OrderByDescending(p => p.PaymentDate)
            .FirstOrDefault();

        var latestTx = booking.PaymentTransactions?
            .OrderByDescending(t => t.NgayTao)
            .FirstOrDefault(t => t.TrangThai == "SUCCESS");

        var amountPaid = booking.PaidAmount > 0 
            ? (latestPayment?.Amount ?? latestTx?.SoTienGhiNhan ?? booking.PaidAmount)
            : 0;

        var remaining = Math.Max(0, (booking.EstimatedTotal ?? amountPaid) - booking.PaidAmount);
        var paymentDate = latestPayment?.PaymentDate ?? latestTx?.NgayTao ?? DateTime.Now;
        var orderCodeStr = latestPayment?.TransactionCode ?? latestTx?.OrderCode.ToString() ?? booking.BookingCode;

        var invoiceDto = new DTOs.Invoice.InvoicePdfDto
        {
            InvoiceNumber = $"HDDT-{booking.BookingCode}-01",
            BookingCode = booking.BookingCode,
            CustomerName = booking.Customer.FullName,
            CustomerPhone = booking.Customer.PhoneNumber,
            CustomerEmail = booking.Customer.Email ?? string.Empty,
            PaymentType = booking.PaidAmount >= (booking.EstimatedTotal ?? 0) && booking.PaidAmount > 0
                ? "Thanh toán toàn bộ (100%)" 
                : "Đặt cọc (10%)",
            PaymentMethod = latestPayment?.PaymentMethod ?? "VIETQR_PAYOS",
            TransactionCode = orderCodeStr,
            InvoiceDate = paymentDate,
            EventDate = booking.HallSchedule?.Date ?? DateTime.Today,
            Shift = booking.HallSchedule?.Shift ?? "Tối",
            HallName = booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
            GuestCount = booking.GuestCount,
            TotalTables = (booking.GuestCount + 9) / 10,
            MenuName = booking.Menu?.MenuName,
            DecorName = booking.DecorationPackage?.TenGoi,
            TotalEstimatedCost = booking.EstimatedTotal ?? amountPaid,
            AmountPaid = amountPaid,
            AccumulatedPaid = booking.PaidAmount,
            RemainingDebt = remaining,
            VerificationUrl = $"https://localhost:7102/MyBooking?bookingCode={Uri.EscapeDataString(booking.BookingCode)}&phone={Uri.EscapeDataString(booking.Customer.PhoneNumber)}",
            IsDigitallySigned = true,
            SignedAt = DateTime.Now
        };

        byte[] unsignedPdf = _invoicePdfGenerator.Generate(invoiceDto);
        var signResult = _signatureService.SignPdfDocument(unsignedPdf, invoiceDto.InvoiceNumber);

        return (signResult.SignedPdfBytes, $"HoaDonDienTu_{booking.BookingCode}.pdf", signResult);
    }
    public async Task<CreateContractPayOsLinkResponseDto> CreateContractPaymentAsync(
        CreateContractPayOsLinkRequestDto request,
        int? managerUserId = null)
    {
        if (request.ContractId <= 0)
            throw new ArgumentException("Mã hợp đồng không hợp lệ.", nameof(request.ContractId));

        if (request.Amount <= 0)
            throw new ArgumentException("Số tiền thanh toán phải lớn hơn 0.", nameof(request.Amount));

        var validTypes = new[] { PaymentTypes.Installment, PaymentTypes.Settlement };
        var normalizedType = validTypes.FirstOrDefault(t => string.Equals(t, request.PaymentType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? PaymentTypes.Installment;

        var contract = await _context.Contracts
            .Include(c => c.Status)
            .Include(c => c.Booking).ThenInclude(b => b.Customer)
            .Include(c => c.Payments).ThenInclude(p => p.Status)
            .SingleOrDefaultAsync(c => c.ContractId == request.ContractId);

        if (contract == null)
            throw new KeyNotFoundException("Không tìm thấy hợp đồng này.");

        if (contract.Status.StatusCode != ContractStatusCodes.Active)
            throw new InvalidOperationException($"Chỉ có thể tạo thanh toán cho hợp đồng đang có hiệu lực (Hiện tại: {contract.Status.StatusName}).");

        // SEC-05: Kiểm tra trần số tiền thanh toán không được vượt quá số dư công nợ còn lại
        var paidSuccess = contract.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == PaymentStatusCodes.Success)
            .Sum(p => p.Amount);
        var remainingBalance = Math.Max(0, contract.TotalValue - paidSuccess);

        if (remainingBalance <= 0)
            throw new InvalidOperationException("Hợp đồng này đã được thanh toán đầy đủ 100%, không còn số dư cần thanh toán.");

        if (request.Amount > remainingBalance)
            throw new InvalidOperationException($"Số tiền thanh toán ({request.Amount:N0} đ) vượt quá số dư công nợ còn lại của hợp đồng ({remainingBalance:N0} đ).");

        if (request.Amount < 2000)
            throw new InvalidOperationException("Số tiền thanh toán tối thiểu là 2.000 đ.");

        decimal businessAmount = request.Amount;
        int actualAmount;
        if (_options.Value.IsDemoMode)
        {
            int ratio = _options.Value.DemoScaleRatio > 0 ? _options.Value.DemoScaleRatio : 10000;
            actualAmount = Math.Max(2000, (int)Math.Round(businessAmount / ratio, MidpointRounding.AwayFromZero));
        }
        else
        {
            actualAmount = (int)businessAmount;
        }

        long orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        string typeShort = normalizedType == PaymentTypes.Installment ? "Dot 2" : "Quyet toan";
        string description = $"HD{contract.ContractId} {typeShort}";
        if (description.Length > 25) description = description.Substring(0, 25);

        var items = new List<PaymentLinkItem>
        {
            new PaymentLinkItem
            {
                Name = $"TT {contract.ContractCode}",
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

        CreatePaymentLinkResponse paymentResult;
        try
        {
            paymentResult = await _payOSClient.PaymentRequests.CreateAsync(paymentRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gọi PayOS SDK PaymentRequests.CreateAsync cho ContractId={ContractId}", contract.ContractId);
            throw new InvalidOperationException($"Không thể tạo liên kết VietQR lúc này: {ex.Message}", ex);
        }

        var txType = normalizedType == PaymentTypes.Installment
            ? PaymentTransactionTypes.Installment
            : PaymentTransactionTypes.Settlement;

        var transaction = new PaymentTransactionEntity
        {
            DatTiecId = contract.BookingId,
            TransactionType = txType,
            OrderCode = orderCode,
            SoTienGiaoDich = actualAmount,
            SoTienGhiNhan = businessAmount,
            PhuongThuc = "VIETQR_PAYOS",
            TrangThai = "PENDING",
            NgayTao = DateTime.Now
        };
        _context.PaymentTransactions.Add(transaction);

        var pendingStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Pending);
        var payment = new Payment
        {
            ContractId = contract.ContractId,
            PaymentType = normalizedType,
            Amount = businessAmount,
            PaymentDate = DateTime.Now,
            PaymentMethod = "VIETQR_PAYOS",
            TransactionCode = orderCode.ToString(),
            StatusId = pendingStatusId
        };
        _context.Payments.Add(payment);

        await _context.SaveChangesAsync();

        // Gửi email thông báo thanh toán hợp đồng từ xa kèm liên kết VietQR PayOS cho khách hàng
        if (!string.IsNullOrWhiteSpace(contract.Booking?.Customer?.Email))
        {
            var customerEmail = contract.Booking.Customer.Email;
            var customerName = contract.Booking.Customer.FullName;
            var bookingCode = contract.Booking.BookingCode;
            var contractCode = contract.ContractCode;
            var checkoutUrl = paymentResult.CheckoutUrl ?? string.Empty;
            var eventDate = contract.Booking.HallSchedule?.Date ?? DateTime.Today.AddDays(30);
            var deadline = eventDate.AddDays(-7);
            var shift = contract.Booking.HallSchedule?.Shift ?? "Tối";
            var hallName = contract.Booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside";
            var customerPhone = contract.Booking.Customer.PhoneNumber;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _emailService.SendContractPaymentRequestEmailAsync(
                        customerEmail, customerName, bookingCode, contractCode,
                        normalizedType, businessAmount, checkoutUrl, deadline,
                        eventDate, shift, hallName, customerPhone);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi gửi email thông báo thanh toán hợp đồng cho {Email}", customerEmail);
                }
            });
        }

        return new CreateContractPayOsLinkResponseDto
        {
            OrderCode = orderCode,
            QrCode = paymentResult.QrCode ?? string.Empty,
            CheckoutUrl = paymentResult.CheckoutUrl ?? string.Empty,
            AccountNumber = paymentResult.AccountNumber,
            AccountName = paymentResult.AccountName,
            BankBin = paymentResult.Bin,
            BankName = paymentResult.Bin,
            Description = description,
            BusinessAmount = businessAmount,
            ActualAmount = actualAmount,
            IsDemoMode = _options.Value.IsDemoMode,
            ExpiresAt = DateTime.Now.AddMinutes(15)
        };
    }

    public async Task<PaymentStatusResponseDto> CheckContractPaymentStatusAsync(long orderCode)
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

        if (tx.TrangThai == "PENDING")
        {
            try
            {
                PaymentLink linkInfo = await _payOSClient.PaymentRequests.GetAsync(orderCode);
                var statusStr = linkInfo?.Status.ToString();
                if (linkInfo != null && string.Equals(statusStr, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Fallback Sync Contract: PayOS báo PAID cho OrderCode={OrderCode}, tự động hoàn tất giao dịch.", orderCode);
                    await CompleteContractPaymentTransactionAsync(tx, orderCode, "Auto-polled from PayOS PaymentRequests.GetAsync");
                }
                else if (linkInfo != null && string.Equals(statusStr, "CANCELLED", StringComparison.OrdinalIgnoreCase))
                {
                    tx.TrangThai = "CANCELLED";
                    var payment = await _context.Payments.SingleOrDefaultAsync(p => p.TransactionCode == orderCode.ToString());
                    if (payment != null)
                    {
                        var cancelledStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Cancelled);
                        payment.StatusId = cancelledStatusId;
                    }
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
            BookingCode = tx.Booking?.BookingCode ?? string.Empty,
            Status = tx.TrangThai,
            BusinessDeposit = tx.SoTienGhiNhan,
            ActualAmount = tx.SoTienGiaoDich,
            PaidAt = tx.NgayThanhToan,
            Message = tx.TrangThai == "PAID"
                ? "Thanh toán hợp đồng thành công!"
                : (tx.TrangThai == "CANCELLED" ? "Giao dịch đã bị hủy." : "Đang chờ thanh toán...")
        };
    }

    private async Task CompleteContractPaymentTransactionAsync(PaymentTransactionEntity tx, long orderCode, string payload)
    {
        await using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            tx.TrangThai = "PAID";
            tx.NgayThanhToan = DateTime.Now;
            tx.PayloadWebhook = payload;

            var booking = await _context.WeddingBookings
                .Include(b => b.Customer)
                .Include(b => b.HallSchedule).ThenInclude(s => s.Hall)
                .Include(b => b.Menu)
                .Include(b => b.DecorationPackage)
                .Include(b => b.Contract).ThenInclude(c => c!.Payments)
                .SingleAsync(b => b.BookingId == tx.DatTiecId);

            booking.PaidAmount = (booking.PaidAmount) + tx.SoTienGhiNhan;

            var successStatusId = await _statusService.GetStatusIdAsync(StatusGroups.Payment, PaymentStatusCodes.Success);
            var payment = await _context.Payments
                .SingleOrDefaultAsync(p => p.TransactionCode == orderCode.ToString());

            if (payment != null)
            {
                payment.StatusId = successStatusId;
                payment.PaymentDate = DateTime.Now;
            }
            else if (booking.Contract != null)
            {
                payment = new Payment
                {
                    ContractId = booking.Contract.ContractId,
                    PaymentType = tx.TransactionType == PaymentTransactionTypes.Settlement ? PaymentTypes.Settlement : PaymentTypes.Installment,
                    Amount = tx.SoTienGhiNhan,
                    PaymentDate = DateTime.Now,
                    PaymentMethod = "VIETQR_PAYOS",
                    TransactionCode = orderCode.ToString(),
                    StatusId = successStatusId
                };
                _context.Payments.Add(payment);
            }

            _context.AuditLogs.Add(new AuditLog
            {
                UserId = null,
                Action = "PAYMENT",
                EntityName = AuditEntityNames.Contract,
                EntityId = booking.Contract?.ContractId ?? booking.BookingId,
                OldData = "OrderCode=" + orderCode + ";TrangThai=PENDING",
                NewData = "OrderCode=" + orderCode + ";TrangThai=PAID;Amount=" + tx.SoTienGhiNhan,
                Timestamp = DateTime.Now,
                Notes = "Đã thanh toán thành công " + tx.SoTienGhiNhan.ToString("N0") + " đ qua VietQR PayOS cho hợp đồng."
            });

            await _context.SaveChangesAsync();
            await dbTransaction.CommitAsync();
            _logger.LogInformation("Hoàn tất thanh toán hợp đồng OrderCode={OrderCode}, Số tiền={Amount:N0} đ", orderCode, tx.SoTienGhiNhan);

            // Gửi email xác nhận thanh toán hợp đồng kèm Hóa đơn điện tử có chữ ký số (PAdES)
            if (!string.IsNullOrWhiteSpace(booking.Customer?.Email))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var contractTotal = booking.Contract?.TotalValue ?? booking.EstimatedTotal ?? tx.SoTienGhiNhan;
                        var remaining = Math.Max(0, contractTotal - booking.PaidAmount);
                        var isFullSettlement = remaining <= 0 || booking.PaidAmount >= contractTotal;
                        var paymentTypeName = isFullSettlement
                            ? "Quyết toán (100%)"
                            : (tx.TransactionType == PaymentTransactionTypes.Settlement ? "Quyết toán sau tiệc" : "Thanh toán Đợt 2 (50%)");

                        var invoiceDto = new DTOs.Invoice.InvoicePdfDto
                        {
                            InvoiceNumber = $"HDDT-{booking.BookingCode}-02",
                            BookingCode = booking.BookingCode,
                            CustomerName = booking.Customer.FullName,
                            CustomerPhone = booking.Customer.PhoneNumber,
                            CustomerEmail = booking.Customer.Email ?? string.Empty,
                            PaymentType = paymentTypeName,
                            PaymentMethod = "VIETQR_PAYOS",
                            TransactionCode = orderCode.ToString(),
                            InvoiceDate = DateTime.Now,
                            EventDate = booking.HallSchedule?.Date ?? DateTime.Today,
                            Shift = booking.HallSchedule?.Shift ?? "Tối",
                            HallName = booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                            GuestCount = booking.GuestCount,
                            TotalTables = (booking.GuestCount + 9) / 10,
                            MenuName = booking.Menu?.MenuName,
                            DecorName = booking.DecorationPackage?.TenGoi,
                            TotalEstimatedCost = contractTotal,
                            AmountPaid = tx.SoTienGhiNhan,
                            AccumulatedPaid = booking.PaidAmount,
                            RemainingDebt = remaining,
                            VerificationUrl = $"https://localhost:7102/MyBooking?bookingCode={Uri.EscapeDataString(booking.BookingCode)}&phone={Uri.EscapeDataString(booking.Customer.PhoneNumber)}",
                            IsDigitallySigned = true,
                            SignedAt = DateTime.Now
                        };

                        byte[] unsignedPdf = _invoicePdfGenerator.Generate(invoiceDto);
                        var signResult = _signatureService.SignPdfDocument(unsignedPdf, invoiceDto.InvoiceNumber);

                        if (isFullSettlement)
                        {
                            await _emailService.SendFullPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                booking.Contract?.ContractCode ?? $"HD-{booking.BookingCode}",
                                booking.PaidAmount,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"HoaDonDienTu_{booking.BookingCode}_QuyetToan.pdf");
                        }
                        else
                        {
                            await _emailService.SendContractPaymentSuccessEmailAsync(
                                booking.Customer.Email!,
                                booking.Customer.FullName,
                                booking.BookingCode,
                                booking.Contract?.ContractCode ?? $"HD-{booking.BookingCode}",
                                paymentTypeName,
                                tx.SoTienGhiNhan,
                                remaining,
                                booking.HallSchedule?.Date ?? DateTime.Today,
                                booking.HallSchedule?.Shift ?? "Tối",
                                booking.HallSchedule?.Hall?.HallName ?? "Sảnh tiệc Riverside",
                                booking.Customer.PhoneNumber,
                                signResult.SignedPdfBytes,
                                $"HoaDonDienTu_{booking.BookingCode}_Dot2.pdf");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Lỗi gửi email xác nhận thanh toán hợp đồng kèm chữ ký số cho BookingCode={BookingCode}", booking.BookingCode);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi hoàn tất thanh toán hợp đồng OrderCode={OrderCode}", orderCode);
            throw;
        }
    }

    public async Task<CreateContractPayOsLinkResponseDto> CreateCustomerContractPaymentAsync(
        CreateCustomerContractPaymentRequestDto request, int? userId = null)
    {
        var query = _context.WeddingBookings
            .Include(b => b.Customer)
            .Include(b => b.Status)
            .Include(b => b.Contract).ThenInclude(c => c!.Status)
            .Include(b => b.Contract).ThenInclude(c => c!.Payments).ThenInclude(p => p.Status)
            .AsQueryable();

        WeddingBooking? booking;
        if (request.BookingId > 0)
            booking = await query.SingleOrDefaultAsync(b => b.BookingId == request.BookingId);
        else
            booking = await query.SingleOrDefaultAsync(b => b.BookingCode == request.BookingCode!.Trim());

        if (booking == null)
            throw new KeyNotFoundException("Không tìm thấy đơn đặt tiệc cưới này.");

        // Xác thực quyền sở hữu
        if (userId.HasValue && userId.Value > 0)
        {
            if (booking.Customer.UserId != userId.Value)
            {
                if (!IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
                    throw new UnauthorizedAccessException("Bạn không có quyền thực hiện thanh toán cho đơn tiệc này.");
            }
        }
        else
        {
            if (!IsGuestVerified(booking, request.BookingCode, request.PhoneNumber))
                throw new UnauthorizedAccessException("Thông tin mã đặt tiệc hoặc số điện thoại không khớp với hồ sơ.");
        }

        if (booking.Contract == null)
            throw new InvalidOperationException("Đơn tiệc chưa có hợp đồng để thanh toán.");

        if (booking.Contract.Status != null && booking.Contract.Status.StatusCode != ContractStatusCodes.Active)
            throw new InvalidOperationException($"Chỉ có thể thanh toán cho hợp đồng đang có hiệu lực (Hiện tại: {booking.Contract.Status.StatusName}).");

        var paidSuccess = booking.Contract.Payments
            .Where(p => p.Status != null && p.Status.StatusCode == PaymentStatusCodes.Success)
            .Sum(p => p.Amount);
        var remainingBalance = Math.Max(0, booking.Contract.TotalValue - paidSuccess);

        if (remainingBalance <= 0)
            throw new InvalidOperationException("Hợp đồng này đã được thanh toán đầy đủ 100%, không còn số dư cần thanh toán.");

        // Mặc định Đợt 2 là 50% tổng giá trị hợp đồng (hoặc toàn bộ số dư còn lại nếu còn ít hơn)
        decimal amountToPay;
        if (request.Amount.HasValue && request.Amount.Value > 0)
        {
            if (request.Amount.Value > remainingBalance)
                throw new InvalidOperationException($"Số tiền thanh toán ({request.Amount.Value:N0} đ) vượt quá số dư công nợ còn lại của hợp đồng ({remainingBalance:N0} đ).");
            amountToPay = request.Amount.Value;
        }
        else
        {
            amountToPay = Math.Min(remainingBalance, Math.Round(booking.Contract.TotalValue * 0.50m, 0));
        }

        var contractRequest = new CreateContractPayOsLinkRequestDto
        {
            ContractId = booking.Contract.ContractId,
            Amount = amountToPay,
            PaymentType = PaymentTypes.Installment
        };

        return await CreateContractPaymentAsync(contractRequest, userId);
    }
}
