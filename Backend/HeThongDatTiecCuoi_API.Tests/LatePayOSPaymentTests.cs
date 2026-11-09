using System.Security.Claims;
using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PayOS;
using Xunit;
using AppPayOSOptions = HeThongDatTiecCuoi_API.Options.PayOSOptions;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class LatePayOSPaymentTests
{
    private const int PendingBookingStatusId = 101;
    private const int ConfirmedBookingStatusId = 102;
    private const int CancelledBookingStatusId = 103;
    private const int AvailableScheduleStatusId = 201;
    private const int BookedScheduleStatusId = 202;
    private const int DraftContractStatusId = 301;
    private const int SuccessfulPaymentStatusId = 401;

    [Fact]
    public async Task TC01_CancelWhilePaymentPending_CancelsBookingAndReleasesSchedule()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);
        var controller = CreateCancellationController(db);

        var result = await controller.CancelBooking(1, "Khách đổi kế hoạch", "DT-LATE-001", "0901234567");

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.FindAsync(1))!.StatusId);
        Assert.Equal(AvailableScheduleStatusId, (await db.HallSchedules.FindAsync(1))!.StatusId);
        Assert.Equal(PaymentTransactionStatusCodes.Pending, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Single(await db.AuditLogs.Where(item => item.Action == AuditActions.BookingHoldCancelled).ToListAsync());
    }

    [Fact]
    public async Task TC02_LatePaymentAfterCancellation_OnlyCreatesReconciliationEvidence()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 10_000, "valid late webhook");

        var booking = await db.WeddingBookings.Include(item => item.Contract).SingleAsync();
        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(CancelledBookingStatusId, booking.StatusId);
        Assert.Equal(0, booking.PaidAmount);
        Assert.Null(booking.Contract);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaidAfterBookingCancelled, transaction.ReconciliationData);
        Assert.NotNull(transaction.NgayThanhToan);
        var status = await service.GetPaymentStatusAsync(900001);
        Assert.Equal(
            "Thanh toán được ghi nhận sau khi đơn đặt tiệc đã hủy. Giao dịch đang chờ bộ phận quản lý đối soát.",
            status.Message);
    }

    [Fact]
    public async Task TC03_DuplicateLateWebhook_IsIdempotent()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 10_000, "webhook 1");
        await service.ProcessConfirmedDepositAsync(900001, 10_000, "webhook duplicate");

        Assert.Equal(0, (await db.WeddingBookings.SingleAsync()).PaidAmount);
        Assert.Empty(await db.Payments.ToListAsync());
        Assert.Single(await db.AuditLogs.Where(item =>
            item.Action == AuditActions.Payment &&
            item.NewData != null && item.NewData.Contains(PaymentTransactionStatusCodes.ReconciliationRequired)).ToListAsync());
    }

    [Fact]
    public async Task TC04_PollingAfterCancellation_UsesSameReconciliationPath()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(
            900001, 10_000, "Auto-polled from PayOS PaymentRequests.GetAsync");

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.SingleAsync()).StatusId);
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task TC05_NormalSuccessfulDeposit_ConfirmsAndRecordsExactlyOnce()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 10_000, "valid webhook");
        await service.ProcessConfirmedDepositAsync(900001, 10_000, "duplicate webhook");

        var booking = await db.WeddingBookings.SingleAsync();
        Assert.Equal(ConfirmedBookingStatusId, booking.StatusId);
        Assert.Equal(1_000_000, booking.PaidAmount);
        Assert.Equal(PaymentTransactionStatusCodes.Paid, (await db.PaymentTransactions.SingleAsync()).TrangThai);
        Assert.Single(await db.Contracts.ToListAsync());
        Assert.Single(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task TC06_ConcurrentPaymentAndCancellation_EndsInOneConsistentOutcome()
    {
        var root = new InMemoryDatabaseRoot();
        var databaseName = $"late-payment-race-{Guid.NewGuid()}";
        var options = CreateOptions(databaseName, root);
        await using (var seed = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked, options)) { }

        await using var cancellationDb = new ApplicationDbContext(options);
        await using var paymentDb = new ApplicationDbContext(options);
        var controller = CreateCancellationController(cancellationDb);
        var service = CreatePaymentService(paymentDb);

        var cancellationTask = controller.CancelBooking(1, "Hủy đồng thời", "DT-LATE-001", "0901234567");
        var paymentTask = service.ProcessConfirmedDepositAsync(900001, 10_000, "concurrent webhook");
        await Task.WhenAll(cancellationTask, paymentTask);

        await using var assertDb = new ApplicationDbContext(options);
        var booking = await assertDb.WeddingBookings.SingleAsync();
        var transaction = await assertDb.PaymentTransactions.SingleAsync();
        var payments = await assertDb.Payments.CountAsync();

        var cancellationWon = booking.StatusId == CancelledBookingStatusId &&
                              transaction.TrangThai == PaymentTransactionStatusCodes.ReconciliationRequired &&
                              booking.PaidAmount == 0 && payments == 0;
        var paymentWon = booking.StatusId == ConfirmedBookingStatusId &&
                         transaction.TrangThai == PaymentTransactionStatusCodes.Paid &&
                         booking.PaidAmount == 1_000_000 && payments == 1;
        Assert.True(cancellationWon || paymentWon,
            $"Inconsistent result: booking={booking.StatusId}, tx={transaction.TrangThai}, paid={booking.PaidAmount}, payments={payments}");
    }

    [Fact]
    public async Task TC07_InvalidPaymentAmount_GoesToReconciliationWithoutConfirming()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Pending, HallScheduleStatusCodes.Booked);
        var service = CreatePaymentService(db);

        await service.ProcessConfirmedDepositAsync(900001, 9_999, "wrong amount webhook");

        var transaction = await db.PaymentTransactions.SingleAsync();
        var booking = await db.WeddingBookings.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaymentAmountMismatch, transaction.ReconciliationData);
        Assert.Equal(PendingBookingStatusId, booking.StatusId);
        Assert.Equal(0, booking.PaidAmount);
        Assert.Empty(await db.Contracts.ToListAsync());
        Assert.Empty(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task TC08_HallReassignedAfterCancellation_LatePaymentDoesNotTouchNewBookingSchedule()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Booked);
        var confirmedStatus = await db.Statuses.SingleAsync(item => item.StatusId == ConfirmedBookingStatusId);
        var customer = new Customer { CustomerId = 2, FullName = "Khách B", PhoneNumber = "0907654321", DataStatusId = 1 };
        db.Customers.Add(customer);
        db.WeddingBookings.Add(new WeddingBooking
        {
            BookingId = 2,
            BookingCode = "DT-NEW-002",
            CustomerId = 2,
            HallScheduleId = 1,
            StatusId = ConfirmedBookingStatusId,
            Status = confirmedStatus,
            GuestCount = 100,
            BookedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        await CreatePaymentService(db).ProcessConfirmedDepositAsync(900001, 10_000, "late webhook");

        Assert.Equal(BookedScheduleStatusId, (await db.HallSchedules.FindAsync(1))!.StatusId);
        Assert.Equal(ConfirmedBookingStatusId, (await db.WeddingBookings.FindAsync(2))!.StatusId);
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.FindAsync(1))!.StatusId);
    }

    [Fact]
    public async Task CancelPaymentLinkFailure_IsRecordedWithoutUndoingBookingCancellation()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db, new ThrowingCancellationClient());

        await service.CancelPendingDepositLinksAsync(1, "Booking cancelled");

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Pending, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PayOsCancelRequestFailed, transaction.ReconciliationData);
        Assert.Equal(CancelledBookingStatusId, (await db.WeddingBookings.SingleAsync()).StatusId);
    }

    [Fact]
    public async Task CancelPaymentLinkRequest_DoesNotClaimPayOsCancellationWasConfirmed()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var service = CreatePaymentService(db, new SuccessfulCancellationClient());

        await service.CancelPendingDepositLinksAsync(1, "Booking cancelled");

        var transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.Pending, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PayOsCancelRequested, transaction.ReconciliationData);
    }

    [Fact]
    public async Task OutOfOrderPaidWebhook_AfterCancelledStatus_StillRecordsReceivedMoneyForReconciliation()
    {
        await using var db = await CreateDatabaseAsync(BookingStatusCodes.Cancelled, HallScheduleStatusCodes.Available);
        var transaction = await db.PaymentTransactions.SingleAsync();
        transaction.TrangThai = PaymentTransactionStatusCodes.Cancelled;
        await db.SaveChangesAsync();

        await CreatePaymentService(db).ProcessConfirmedDepositAsync(900001, 10_000, "late paid webhook");

        transaction = await db.PaymentTransactions.SingleAsync();
        Assert.Equal(PaymentTransactionStatusCodes.ReconciliationRequired, transaction.TrangThai);
        Assert.Contains(PaymentReconciliationReasons.PaidAfterBookingCancelled, transaction.ReconciliationData);
    }

    private static MyBookingsController CreateCancellationController(ApplicationDbContext db)
    {
        var controller = new MyBookingsController(db, new TestStatusService(), new CoordinationStub());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        return controller;
    }

    private static PayOSPaymentService CreatePaymentService(
        ApplicationDbContext db,
        IPayOSPaymentLinkCancellation? cancellation = null) =>
        new(
            db,
            new PayOSClient("test-client", "test-api-key", "test-checksum-key"),
            Microsoft.Extensions.Options.Options.Create(new AppPayOSOptions { IsDemoMode = true, DemoScaleRatio = 100 }),
            new TestStatusService(),
            null!,
            null!,
            null!,
            null!,
            NullLogger<PayOSPaymentService>.Instance,
            cancellation);

    private static DbContextOptions<ApplicationDbContext> CreateOptions(
        string? databaseName = null,
        InMemoryDatabaseRoot? root = null) =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString(), root ?? new InMemoryDatabaseRoot())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

    private static async Task<ApplicationDbContext> CreateDatabaseAsync(
        string bookingStatusCode,
        string scheduleStatusCode,
        DbContextOptions<ApplicationDbContext>? options = null)
    {
        var db = new ApplicationDbContext(options ?? CreateOptions());
        var statuses = new[]
        {
            new Status { StatusId = PendingBookingStatusId, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Pending, StatusName = "Chờ xác nhận" },
            new Status { StatusId = ConfirmedBookingStatusId, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Confirmed, StatusName = "Đã xác nhận" },
            new Status { StatusId = CancelledBookingStatusId, StatusGroup = StatusGroups.Booking, StatusCode = BookingStatusCodes.Cancelled, StatusName = "Đã hủy" },
            new Status { StatusId = AvailableScheduleStatusId, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Available, StatusName = "Trống" },
            new Status { StatusId = BookedScheduleStatusId, StatusGroup = StatusGroups.HallSchedule, StatusCode = HallScheduleStatusCodes.Booked, StatusName = "Đã đặt" },
            new Status { StatusId = DraftContractStatusId, StatusGroup = StatusGroups.Contract, StatusCode = ContractStatusCodes.Draft, StatusName = "Nháp" },
            new Status { StatusId = SuccessfulPaymentStatusId, StatusGroup = StatusGroups.Payment, StatusCode = PaymentStatusCodes.Success, StatusName = "Thành công" }
        };
        db.Statuses.AddRange(statuses);
        db.DataStatuses.Add(new DataStatus { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });

        var bookingStatus = statuses.Single(item => item.StatusCode == bookingStatusCode && item.StatusGroup == StatusGroups.Booking);
        var scheduleStatus = statuses.Single(item => item.StatusCode == scheduleStatusCode && item.StatusGroup == StatusGroups.HallSchedule);
        var hall = new Hall
        {
            HallId = 1, HallCode = "H01", HallName = "Sảnh Riverside", MinimumCapacity = 50,
            MaximumCapacity = 500, RentalPrice = 20_000_000, StatusId = AvailableScheduleStatusId, DataStatusId = 1
        };
        var schedule = new HallSchedule
        {
            HallScheduleId = 1, HallId = 1, Hall = hall, Date = DateTime.Today.AddDays(30), Shift = "Ca tối",
            StatusId = scheduleStatus.StatusId, Status = scheduleStatus, DataStatusId = 1
        };
        var customer = new Customer
        {
            CustomerId = 1, FullName = "Nguyễn Khách", PhoneNumber = "0901234567", DataStatusId = 1
        };
        var booking = new WeddingBooking
        {
            BookingId = 1,
            BookingCode = "DT-LATE-001",
            CustomerId = 1,
            Customer = customer,
            HallScheduleId = 1,
            HallSchedule = schedule,
            StatusId = bookingStatus.StatusId,
            Status = bookingStatus,
            GuestCount = 200,
            EstimatedTotal = 10_000_000,
            PaidAmount = 0,
            BookedAt = DateTime.Now
        };
        db.Halls.Add(hall);
        db.HallSchedules.Add(schedule);
        db.Customers.Add(customer);
        db.WeddingBookings.Add(booking);
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            GiaoDichId = 1,
            DatTiecId = 1,
            Booking = booking,
            TransactionType = PaymentTransactionTypes.Deposit,
            OrderCode = 900001,
            SoTienGiaoDich = 10_000,
            SoTienGhiNhan = 1_000_000,
            TrangThai = PaymentTransactionStatusCodes.Pending,
            NgayTao = DateTime.Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return db;
    }

    private sealed class TestStatusService : IStatusService
    {
        public Task<int> GetStatusIdAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult((statusGroup, statusCode) switch
            {
                (StatusGroups.Booking, BookingStatusCodes.Pending) => PendingBookingStatusId,
                (StatusGroups.Booking, BookingStatusCodes.Confirmed) => ConfirmedBookingStatusId,
                (StatusGroups.Booking, BookingStatusCodes.Cancelled) => CancelledBookingStatusId,
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Available) => AvailableScheduleStatusId,
                (StatusGroups.HallSchedule, HallScheduleStatusCodes.Booked) => BookedScheduleStatusId,
                (StatusGroups.Contract, ContractStatusCodes.Draft) => DraftContractStatusId,
                (StatusGroups.Payment, PaymentStatusCodes.Success) => SuccessfulPaymentStatusId,
                _ => throw new InvalidOperationException($"Unknown status {statusGroup}/{statusCode}")
            });

        public Task<Status?> GetStatusAsync(string statusGroup, string statusCode, CancellationToken cancellationToken = default) =>
            Task.FromResult<Status?>(null);
    }

    private sealed class ThrowingCancellationClient : IPayOSPaymentLinkCancellation
    {
        public Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default) =>
            throw new HttpRequestException("PayOS unavailable");
    }

    private sealed class SuccessfulCancellationClient : IPayOSPaymentLinkCancellation
    {
        public Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class CoordinationStub : ICoordinationAssignmentService
    {
        public Task<ServiceResult<HallManagerCoordinationListDto>> GetHallManagerListAsync(int userId, HallManagerCoordinationQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAvailabilityDto>>> GetCoordinatorAvailabilityAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<HallManagerCoordinationDetailDto>> GetHallManagerDetailAsync(int userId, int bookingId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> AssignAsync(int userId, int bookingId, CreateCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> ChangeAsync(int userId, int bookingId, ChangeCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<CoordinationMutationResultDto>> CancelAsync(int userId, int bookingId, CancelCoordinationAssignmentRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<ServiceResult<List<CoordinatorAssignedPartyDto>>> GetCoordinatorPartiesAsync(int userId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SynchronizeBookingAssignmentsAsync(int bookingId, string bookingStatusCode, int? actorUserId, string? reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> HasScheduleConflictAsync(int coordinatorEmployeeId, int hallScheduleId, int? excludedBookingId, CancellationToken cancellationToken) => Task.FromResult(false);
    }
}
