using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Controllers;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.EventCoordination;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class CoordinationAssignmentWriteTests
{
    [Fact]
    public async Task Assign_SucceedsInManagedHall_AndUsesSignedInEmployee()
    {
        await using var db = CreateContext(); Seed(db); AddBooking(db, 1, 1, DateTime.Today.AddDays(2), "Ca trưa");
        await db.SaveChangesAsync(); var service = Service(db);

        var result = await service.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);

        Assert.True(result.Succeeded); Assert.Equal(StatusCodes.Status201Created, result.StatusCode);
        var assignment = await db.CoordinationAssignments.SingleAsync();
        Assert.Equal(10, assignment.AssignedByHallManagerEmployeeId);
        Assert.Contains(await db.AuditLogs.ToListAsync(), x => x.Action == AuditActions.CoordinationAssigned);
        Assert.Single(await db.Notifications.Where(x => x.RecipientUserId == 200).ToListAsync());
    }

    [Fact]
    public async Task Assign_RejectsBookingOutsideManagedHall()
    {
        await using var db = CreateContext(); Seed(db); AddBooking(db, 1, 2, DateTime.Today.AddDays(2), "Ca trưa");
        await db.SaveChangesAsync();
        var result = await Service(db).AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);
        Assert.False(result.Succeeded); Assert.Equal(StatusCodes.Status404NotFound, result.StatusCode);
    }

    [Theory]
    [InlineData(BookingStatusCodes.Pending)]
    [InlineData(BookingStatusCodes.Cancelled)]
    [InlineData(BookingStatusCodes.Completed)]
    public async Task Assign_RejectsIneligibleBookingStatus(string status)
    {
        await using var db = CreateContext(); Seed(db); AddBooking(db, 1, 1, DateTime.Today.AddDays(2), "Ca trưa", status);
        await db.SaveChangesAsync();
        var result = await Service(db).AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);
        Assert.False(result.Succeeded); Assert.Equal(StatusCodes.Status409Conflict, result.StatusCode);
    }

    [Fact]
    public async Task Assign_AllowsSameDayDifferentShift_ButRejectsSameShiftAcrossHalls()
    {
        await using var db = CreateContext(); Seed(db, manageSecondHall: true); var date = DateTime.Today.AddDays(3);
        AddBooking(db, 1, 1, date, "Ca trưa"); AddBooking(db, 2, 1, date, "Ca tối"); AddBooking(db, 3, 2, date, "Ca trưa");
        await db.SaveChangesAsync(); var service = Service(db);
        Assert.True((await service.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default)).Succeeded);
        Assert.True((await service.AssignAsync(100, 2, new() { CoordinatorEmployeeId = 20 }, default)).Succeeded);
        var conflict = await service.AssignAsync(100, 3, new() { CoordinatorEmployeeId = 20 }, default);
        Assert.False(conflict.Succeeded); Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Change_CancelsOldAssignment_CreatesNew_AndKeepsHistory()
    {
        await using var db = CreateContext(); Seed(db); AddCoordinator(db, 21, 201, "DP021", "Điều phối 21");
        AddBooking(db, 1, 1, DateTime.Today.AddDays(2), "Ca trưa"); await db.SaveChangesAsync(); var service = Service(db);
        await service.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);
        var result = await service.ChangeAsync(100, 1, new() { CoordinatorEmployeeId = 21, Reason = "Điều chỉnh nhân sự" }, default);
        Assert.True(result.Succeeded); var rows = await db.CoordinationAssignments.Include(x => x.Status).ToListAsync();
        Assert.Equal(2, rows.Count); Assert.Single(rows, x => x.Status.StatusCode == CoordinationAssignmentStatusCodes.Assigned);
        Assert.Single(rows, x => x.Status.StatusCode == CoordinationAssignmentStatusCodes.Cancelled);
    }

    [Fact]
    public async Task Cancel_RequiresReason_AndSoftCancels()
    {
        await using var db = CreateContext(); Seed(db); AddBooking(db, 1, 1, DateTime.Today.AddDays(2), "Ca trưa");
        await db.SaveChangesAsync(); var service = Service(db); await service.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);
        var invalid = await service.CancelAsync(100, 1, new() { Reason = "" }, default);
        Assert.Equal(StatusCodes.Status400BadRequest, invalid.StatusCode);
        var valid = await service.CancelAsync(100, 1, new() { Reason = "Khách đổi kế hoạch" }, default);
        Assert.True(valid.Succeeded); Assert.Equal(CoordinationAssignmentStatusCodes.Cancelled,
            (await db.CoordinationAssignments.Include(x => x.Status).SingleAsync()).Status.StatusCode);
    }

    [Fact]
    public async Task BookingCancellation_SynchronizesAssignment_AndCoordinatorList()
    {
        await using var db = CreateContext(); Seed(db); AddBooking(db, 1, 1, DateTime.Today.AddDays(2), "Ca trưa");
        await db.SaveChangesAsync(); var service = Service(db); await service.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);
        await service.SynchronizeBookingAssignmentsAsync(1, BookingStatusCodes.Cancelled, 100, "Hủy tiệc", default);
        await db.SaveChangesAsync();
        Assert.Equal(CoordinationAssignmentStatusCodes.Cancelled,
            (await db.CoordinationAssignments.Include(x => x.Status).SingleAsync()).Status.StatusCode);
        Assert.Empty((await service.GetCoordinatorPartiesAsync(200, default)).Value!);
    }

    [Fact]
    public async Task ChangingCurrentHallManager_ChangesWriteScope()
    {
        await using var db = CreateContext(); Seed(db); AddHallManager(db, 11, 101, "QLS011", "Quản lý mới");
        var current = db.HallManagerAssignments.Local.Single(); current.ToDate = DateTime.Today.AddDays(-1);
        db.HallManagerAssignments.Add(new() { HallManagerAssignmentId = 2, HallId = 1, HallManagerEmployeeId = 11, FromDate = DateTime.Today });
        AddBooking(db, 1, 1, DateTime.Today.AddDays(2), "Ca trưa"); await db.SaveChangesAsync(); var service = Service(db);
        Assert.Equal(StatusCodes.Status404NotFound, (await service.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default)).StatusCode);
        Assert.True((await service.AssignAsync(101, 1, new() { CoordinatorEmployeeId = 20 }, default)).Succeeded);
    }

    [Fact]
    public void WriteController_IsHallManagerOnly_AndCoordinatorControllerIsCoordinatorOnly()
    {
        var hall = Assert.Single(typeof(HallManagerCoordinationController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        var coordinator = Assert.Single(typeof(CoordinatorAssignmentsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(RoleNames.HallManager, hall.Roles); Assert.Equal(RoleNames.Coordinator, coordinator.Roles);
    }

    private static ApplicationDbContext CreateContext() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static CoordinationAssignmentService Service(ApplicationDbContext db) => new(db, new NotificationService(db));

    private static void Seed(ApplicationDbContext db, bool manageSecondHall = false)
    {
        db.DataStatuses.Add(new() { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });
        db.Roles.AddRange(new Role { RoleId = 3, RoleName = RoleNames.HallManager }, new Role { RoleId = 4, RoleName = RoleNames.Coordinator });
        db.Statuses.AddRange(
            S(101, StatusGroups.Account, AccountStatusCodes.Active), S(201, StatusGroups.Hall, "ACTIVE"), S(301, StatusGroups.HallSchedule, "BOOKED"),
            S(801, StatusGroups.Booking, BookingStatusCodes.Pending), S(802, StatusGroups.Booking, BookingStatusCodes.Confirmed), S(803, StatusGroups.Booking, BookingStatusCodes.Preparing), S(804, StatusGroups.Booking, BookingStatusCodes.Completed), S(805, StatusGroups.Booking, BookingStatusCodes.Cancelled),
            S(1101, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Assigned), S(1102, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Completed), S(1103, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Cancelled));
        db.Halls.AddRange(Hall(1, "Sảnh 1"), Hall(2, "Sảnh 2"));
        AddHallManager(db, 10, 100, "QLS010", "Quản lý sảnh"); AddCoordinator(db, 20, 200, "DP020", "Điều phối 20");
        db.HallManagerAssignments.Add(new() { HallManagerAssignmentId = 1, HallId = 1, HallManagerEmployeeId = 10, FromDate = DateTime.Today.AddDays(-10) });
        if (manageSecondHall) db.HallManagerAssignments.Add(new() { HallManagerAssignmentId = 3, HallId = 2, HallManagerEmployeeId = 10, FromDate = DateTime.Today.AddDays(-10) });
    }
    private static Status S(int id, string group, string code) => new() { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code };
    private static Hall Hall(int id, string name) => new() { HallId = id, HallCode = $"S{id}", HallName = name, MaximumCapacity = 500, RentalPrice = 1, StatusId = 201, DataStatusId = 1 };
    private static void AddHallManager(ApplicationDbContext db, int employeeId, int userId, string code, string name) => AddEmployee(db, employeeId, userId, code, name, 3);
    private static void AddCoordinator(ApplicationDbContext db, int employeeId, int userId, string code, string name) => AddEmployee(db, employeeId, userId, code, name, 4);
    private static void AddEmployee(ApplicationDbContext db, int employeeId, int userId, string code, string name, byte roleId)
    {
        db.Users.Add(new User { UserId = userId, RoleId = roleId, Email = $"u{userId}@test.local", PasswordHash = "test", StatusId = 101, DataStatusId = 1 });
        db.Employees.Add(new Employee { EmployeeId = employeeId, UserId = userId, EmployeeCode = code, FullName = name, PhoneNumber = $"09{userId:00000000}", DataStatusId = 1 });
    }
    private static void AddBooking(ApplicationDbContext db, int bookingId, int hallId, DateTime date, string shift, string status = BookingStatusCodes.Confirmed)
    {
        db.Customers.Add(new Customer { CustomerId = bookingId, CustomerCode = $"KH{bookingId}", FullName = $"Khách {bookingId}", PhoneNumber = $"090{bookingId:0000000}", DataStatusId = 1 });
        db.HallSchedules.Add(new HallSchedule { HallScheduleId = bookingId, HallId = hallId, Date = date.Date, Shift = shift, StatusId = 301, DataStatusId = 1 });
        var statusId = status switch { BookingStatusCodes.Pending => 801, BookingStatusCodes.Preparing => 803, BookingStatusCodes.Completed => 804, BookingStatusCodes.Cancelled => 805, _ => 802 };
        db.WeddingBookings.Add(new WeddingBooking { BookingId = bookingId, BookingCode = $"DT-{bookingId}", CustomerId = bookingId, HallScheduleId = bookingId, GuestCount = 100, StatusId = statusId, BookedAt = DateTime.Now });
    }
}
