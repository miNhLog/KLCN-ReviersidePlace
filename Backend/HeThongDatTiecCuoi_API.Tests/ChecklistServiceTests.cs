using HeThongDatTiecCuoi_API.Constants;
using HeThongDatTiecCuoi_API.Constants.StatusCodes;
using HeThongDatTiecCuoi_API.Data;
using HeThongDatTiecCuoi_API.DTOs.Checklist;
using HeThongDatTiecCuoi_API.Models;
using HeThongDatTiecCuoi_API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ChecklistServiceTests
{
    [Fact]
    public async Task Assignment_CreatesOneChecklist_AndReassignmentPreservesTasksAndScope()
    {
        await using var f = await Fixture.Create();
        var first = await f.Db.PartyChecklists.Include(c => c.Tasks).SingleAsync();
        Assert.Equal(8, first.Tasks.Count);
        var task = first.Tasks.First(); task.Status = "done"; task.Note = "Đã kiểm tra";
        await f.Db.SaveChangesAsync();
        var result = await f.Assignments.ChangeAsync(100, 1, new() { CoordinatorEmployeeId = 21, Reason = "Bàn giao ca" }, default);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, await f.Db.PartyChecklists.CountAsync());
        Assert.Equal(8, await f.Db.PartyChecklistTasks.CountAsync());
        Assert.Equal("Đã kiểm tra", (await f.Db.PartyChecklistTasks.SingleAsync(t => t.Id == task.Id)).Note);
        Assert.Empty((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.True(Assert.Single((await f.Service.GetAsync(201, RoleNames.Coordinator, default)).Value!.Parties).CanEdit);
        Assert.Equal(404, (await f.Service.ExecuteAsync(200, RoleNames.Coordinator, 1, "task", new() { Version = first.Version, TaskId = task.Id, Status = "todo" }, default)).StatusCode);
    }

    [Fact]
    public async Task Handover_RequiresMandatoryWork_AndManagerReviewDoesNotCompleteBooking()
    {
        await using var f = await Fixture.Create();
        var list = await f.Db.PartyChecklists.Include(c => c.Tasks).SingleAsync();
        Assert.Equal(409, (await f.Run(200, "submit", new() { Version = list.Version })).StatusCode);
        foreach (var t in list.Tasks) { var result = await f.Run(200, "task", new() { Version = list.Version, TaskId = t.Id, Status = "done" }); Assert.True(result.Succeeded, result.Error); }
        Assert.Equal(0, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        Assert.True((await f.Run(200, "submit", new() { Version = list.Version, Note = "Đã hoàn tất" })).Succeeded);
        Assert.Equal("review", list.State);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = list.Version, TaskId = list.Tasks.First().Id, Status = "todo" })).StatusCode);
        Assert.Equal(403, (await f.Run(200, "approve", new() { Version = list.Version })).StatusCode);
        Assert.True((await f.Run(100, "approve", new() { Version = list.Version })).Succeeded);
        Assert.Equal("approved", list.State);
        Assert.Equal(BookingStatusCodes.Confirmed, (await f.Db.WeddingBookings.Include(b => b.Status).SingleAsync()).Status.StatusCode);
        Assert.Contains(await f.Db.PartyChecklistHistory.ToListAsync(), h => h.Action == "APPROVE" && h.ActorUserId == 100);
    }

    [Fact]
    public async Task Return_ReopensOnlySelectedTask_AndRecordsReason()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        foreach (var t in c.Tasks) t.Status = "done";
        c.State = "review"; await f.Db.SaveChangesAsync();
        var result = await f.Run(100, "return", new() { Version = c.Version, TaskId = c.Tasks.First().Id, Note = "Kiểm tra lại thiết bị" });
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("active", c.State); Assert.Equal("Kiểm tra lại thiết bị", c.ReturnReason);
        Assert.Single(c.Tasks, t => t.Status == "todo");
        Assert.Equal(1, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
    }

    [Fact]
    public async Task ScopeAndVersion_RejectForeignManagerForeignTaskAndStaleRequest()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        Assert.Empty((await f.Service.GetAsync(101, RoleNames.HallManager, default)).Value!.Parties);
        Assert.Equal(404, (await f.Service.ExecuteAsync(101, RoleNames.HallManager, 1, "add", new() { Version = c.Version }, default)).StatusCode);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = c.Version, TaskId = 9999, Status = "done" })).StatusCode);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = c.Version - 1, TaskId = c.Tasks.First().Id, Status = "done" })).StatusCode);
        Assert.Equal(403, (await f.Service.GetAsync(200, RoleNames.Manager, default)).StatusCode);
        Assert.All(c.Tasks, t => Assert.Equal("todo", t.Status));
    }

    [Fact]
    public async Task Reschedule_RebasesDeadlines_WithoutResettingProgress()
    {
        await using var f = await Fixture.Create();
        var before = Assert.Single((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync(); c.Tasks.First().Status = "done";
        var schedule = await f.Db.HallSchedules.SingleAsync(); schedule.Date = schedule.Date.AddDays(4);
        await f.Db.SaveChangesAsync();
        var after = Assert.Single((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.Equal(DateTime.Parse(before.Tasks[0].Due).AddDays(4).ToString("yyyy-MM-dd"), after.Tasks[0].Due);
        Assert.Single(after.Tasks, t => t.Status == "done");
    }

    [Fact]
    public async Task Cancellation_KeepsHistory_ButLocksChecklistAndRemovesOutstandingCount()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        Assert.Equal(8, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        var booking = await f.Db.WeddingBookings.SingleAsync(); booking.StatusId = 805;
        await f.Assignments.SynchronizeBookingAssignmentsAsync(1, BookingStatusCodes.Cancelled, 100, "Khách hủy tiệc", default);
        await f.Db.SaveChangesAsync();
        Assert.Equal("cancelled", c.State); Assert.Equal(8, await f.Db.PartyChecklistTasks.CountAsync());
        Assert.Equal(0, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
        Assert.Equal(409, (await f.Run(200, "task", new() { Version = c.Version, TaskId = c.Tasks.First().Id, Status = "done" })).StatusCode);
    }

    [Fact]
    public async Task AddingWorkDuringReview_ReopensChecklist_AndOptionalWorkDoesNotBlockHandover()
    {
        await using var f = await Fixture.Create();
        var c = await f.Db.PartyChecklists.Include(x => x.Tasks).SingleAsync();
        foreach (var t in c.Tasks) t.Status = "done";
        c.State = "review"; await f.Db.SaveChangesAsync();
        var due = RiversideBusinessClock.Today.AddDays(3);
        var added = await f.Run(100, "add", new() { Version = c.Version, Name = "Chuẩn bị thêm bàn chay", Group = "Trước tiệc", DueDate = due, Required = false });
        Assert.True(added.Succeeded, added.Error);
        Assert.Equal("active", c.State); Assert.Equal(9, c.Tasks.Count);
        var dto = Assert.Single((await f.Service.GetAsync(200, RoleNames.Coordinator, default)).Value!.Parties);
        Assert.Equal(due.ToString("yyyy-MM-dd"), dto.Tasks.Single(t => t.Name == "Chuẩn bị thêm bàn chay").Due);
        Assert.True((await f.Run(200, "submit", new() { Version = c.Version })).Succeeded);
        Assert.Equal("review", c.State);
        Assert.Equal(0, (await f.Service.IncompleteCountAsync(200, RoleNames.Coordinator, default)).Value);
    }

    [Fact]
    public async Task ConcurrentTrackedUpdates_DoNotOverwriteFirstWriter()
    {
        await using var f = await Fixture.Create();
        await using var second = new ApplicationDbContext(f.Options);
        var stale = await second.PartyChecklists.Include(c => c.Tasks).SingleAsync();
        var taskId = stale.Tasks.First().Id;
        Assert.True((await f.Run(200, "task", new() { Version = stale.Version, TaskId = taskId, Status = "done" })).Succeeded);
        var result = await new ChecklistService(second).ExecuteAsync(200, RoleNames.Coordinator, 1, "task", new() { Version = stale.Version, TaskId = taskId, Status = "doing" }, default);
        Assert.Equal(409, result.StatusCode);
        await using var verify = new ApplicationDbContext(f.Options);
        Assert.Equal("done", (await verify.PartyChecklistTasks.SingleAsync(t => t.Id == taskId)).Status);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public DbContextOptions<ApplicationDbContext> Options { get; } = new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        public ApplicationDbContext Db { get; }
        public ChecklistService Service { get; }
        public CoordinationAssignmentService Assignments { get; }
        private Fixture() { Db = new(Options); Service = new(Db); Assignments = new(Db, new NotificationService(Db)); }
        public Task<ServiceResult<string>> Run(int user, string action, ChecklistCommand command) => Service.ExecuteAsync(user, user == 100 ? RoleNames.HallManager : RoleNames.Coordinator, 1, action, command, default);
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); var db = f.Db;
            db.DataStatuses.Add(new() { DataStatusId = 1, DataStatusCode = DataStatusCodes.Existing, DataStatusName = "Tồn tại" });
            db.Roles.AddRange(new Role { RoleId = 3, RoleName = RoleNames.HallManager }, new Role { RoleId = 4, RoleName = RoleNames.Coordinator });
            db.Statuses.AddRange(S(101, StatusGroups.Account, AccountStatusCodes.Active), S(201, StatusGroups.Hall, "ACTIVE"), S(301, StatusGroups.HallSchedule, "BOOKED"),
                S(802, StatusGroups.Booking, BookingStatusCodes.Confirmed), S(804, StatusGroups.Booking, BookingStatusCodes.Completed), S(805, StatusGroups.Booking, BookingStatusCodes.Cancelled),
                S(1101, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Assigned), S(1102, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Completed), S(1103, StatusGroups.CoordinationAssignment, CoordinationAssignmentStatusCodes.Cancelled));
            foreach (var (employee, user, role) in new[] { (10, 100, 3), (11, 101, 3), (20, 200, 4), (21, 201, 4) })
            {
                db.Users.Add(new User { UserId = user, RoleId = (byte)role, Email = $"u{user}@test.local", PasswordHash = "test", StatusId = 101, DataStatusId = 1 });
                db.Employees.Add(new Employee { EmployeeId = employee, UserId = user, EmployeeCode = $"NV{employee}", FullName = $"Nhân viên {employee}", PhoneNumber = $"090{user:0000000}", DataStatusId = 1 });
            }
            db.Halls.Add(new Hall { HallId = 1, HallCode = "S1", HallName = "Sảnh 1", MaximumCapacity = 500, RentalPrice = 1, StatusId = 201, DataStatusId = 1 });
            db.HallManagerAssignments.Add(new() { HallId = 1, HallManagerEmployeeId = 10, FromDate = RiversideBusinessClock.Today.AddDays(-10) });
            db.Customers.Add(new Customer { CustomerId = 1, CustomerCode = "KH1", FullName = "Khách 1", PhoneNumber = "0900000001", DataStatusId = 1 });
            db.HallSchedules.Add(new HallSchedule { HallScheduleId = 1, HallId = 1, Date = RiversideBusinessClock.Today.AddDays(2), Shift = "Ca tối", StatusId = 301, DataStatusId = 1 });
            db.WeddingBookings.Add(new WeddingBooking { BookingId = 1, BookingCode = "DT1", CustomerId = 1, HallScheduleId = 1, GuestCount = 100, StatusId = 802, BookedAt = RiversideBusinessClock.Now });
            await db.SaveChangesAsync();
            var assigned = await f.Assignments.AssignAsync(100, 1, new() { CoordinatorEmployeeId = 20 }, default);
            Assert.True(assigned.Succeeded, assigned.Error); return f;
        }
        private static Status S(int id, string group, string code) => new() { StatusId = id, StatusGroup = group, StatusCode = code, StatusName = code };
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
