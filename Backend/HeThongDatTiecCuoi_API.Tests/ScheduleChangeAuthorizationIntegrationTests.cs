using System.Net;
using System.Net.Http.Headers;
using HeThongDatTiecCuoi_API.Models;
using Xunit;

namespace HeThongDatTiecCuoi_API.Tests;

public sealed class ScheduleChangeAuthorizationIntegrationTests : IClassFixture<EventCoordinationApiFactory>
{
    private readonly EventCoordinationApiFactory _factory;
    public ScheduleChangeAuthorizationIntegrationTests(EventCoordinationApiFactory factory) => _factory = factory;

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(RoleNames.Manager, HttpStatusCode.OK)]
    [InlineData(RoleNames.Admin, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.HallManager, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Coordinator, HttpStatusCode.Forbidden)]
    public async Task Manager_schedule_change_api_is_backend_authorized(string? role, HttpStatusCode expected)
    {
        using var client = Client(role);
        using var response = await client.GetAsync("/api/manager/schedule-changes");
        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(null, HttpStatusCode.Unauthorized)]
    [InlineData(RoleNames.HallManager, HttpStatusCode.OK)]
    [InlineData(RoleNames.Manager, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Admin, HttpStatusCode.Forbidden)]
    [InlineData(RoleNames.Coordinator, HttpStatusCode.Forbidden)]
    public async Task Hall_manager_schedule_change_api_is_backend_authorized(string? role, HttpStatusCode expected)
    {
        using var client = Client(role);
        using var response = await client.GetAsync("/api/hall-manager/schedule-changes");
        Assert.Equal(expected, response.StatusCode);
    }

    private HttpClient Client(string? role)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        { AllowAutoRedirect = false, BaseAddress = new Uri("https://localhost") });
        if (role is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(TestRoleAuthenticationHandler.Scheme, role);
        return client;
    }
}
