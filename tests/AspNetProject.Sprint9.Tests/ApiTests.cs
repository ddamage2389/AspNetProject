using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AspNetProject.Bookings.Api;
using AspNetProject.Events.Api;
using AspNetProject.Users.Api;
using AspNetProject.Users.Domain.Entities;
using AspNetProject.Users.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using AspNetProject.Events.Application.Caching;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AspNetProject.Sprint9.Tests;

public sealed class ServiceFactory<T>(string connection) : WebApplicationFactory<T> where T : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Database", connection);
        builder.UseSetting("Workers:Enabled", "false");
        builder.UseSetting("Database:MigrateOnStartup", "false");
        builder.UseSetting("JwtSettings:Secret", "sprint9-tests-shared-jwt-secret-at-least-32-bytes");
        builder.UseSetting("JwtSettings:Issuer", "sprint9-tests");
        builder.UseSetting("JwtSettings:Audience", "sprint9-tests");
        builder.UseSetting("JwtSettings:ExpiryMinutes", "60");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ICache>();
            services.AddSingleton<ICache, TestCache>();
        });
    }
}

[Collection("Sprint9")]
public sealed class ApiTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Shared_JWT_authorization_and_booking_ownership_work_across_APIs()
    {
        using var usersFactory = new ServiceFactory<UsersApiMarker>(fixture.Connection("users"));
        using var eventsFactory = new ServiceFactory<EventsApiMarker>(fixture.Connection("events"));
        using var bookingsFactory = new ServiceFactory<BookingsApiMarker>(fixture.Connection("bookings"));
        using var users = usersFactory.CreateClient();
        using var events = eventsFactory.CreateClient();
        using var bookings = bookingsFactory.CreateClient();
        var login = $"user-{Guid.NewGuid():N}";
        const string password = "testPassword123!";
        Assert.Equal(HttpStatusCode.NoContent, (await users.PostAsJsonAsync("/auth/register", new { login, password, role = "Admin" })).StatusCode);
        var token = await Login(users, login, password);
        var adminLogin = $"admin-{Guid.NewGuid():N}";
        await using (var db = fixture.Users())
        {
            db.Users.Add(User.Create(adminLogin, new PasswordHasher().Hash(password), Role.Admin));
            await db.SaveChangesAsync();
        }
        var adminToken = await Login(users, adminLogin, password);
        var createEvent = new { title = "Integration", startAt = DateTime.UtcNow.AddDays(1), endAt = DateTime.UtcNow.AddDays(2), totalSeats = 10 };
        var top = await events.GetAsync("/events/top");
        Assert.Equal(HttpStatusCode.OK, top.StatusCode);
        Assert.Equal(JsonValueKind.Array, (await top.Content.ReadFromJsonAsync<JsonElement>()).ValueKind);
        Assert.Equal(HttpStatusCode.Unauthorized, (await events.PostAsJsonAsync("/events", createEvent)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bookings.PostAsJsonAsync("/bookings", new { eventId = Guid.NewGuid(), seats = 1 })).StatusCode);
        events.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        Assert.Equal(HttpStatusCode.Forbidden, (await events.PostAsJsonAsync("/events", createEvent)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await events.PutAsJsonAsync($"/events/{Guid.NewGuid()}", createEvent)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await events.DeleteAsync($"/events/{Guid.NewGuid()}")).StatusCode);
        events.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var created = await events.PostAsJsonAsync("/events", createEvent);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var eventId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        bookings.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var bookingResponse = await bookings.PostAsJsonAsync("/bookings", new { eventId, seats = 2 });
        Assert.Equal(HttpStatusCode.Accepted, bookingResponse.StatusCode);
        var bookingId = (await bookingResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await bookings.GetAsync($"/bookings/{bookingId}")).StatusCode);
        var stranger = $"stranger-{Guid.NewGuid():N}";
        await users.PostAsJsonAsync("/auth/register", new { login = stranger, password });
        bookings.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Login(users, stranger, password));
        Assert.Equal(HttpStatusCode.NotFound, (await bookings.GetAsync($"/bookings/{bookingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bookings.DeleteAsync($"/bookings/{bookingId}")).StatusCode);
        bookings.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.NoContent, (await bookings.DeleteAsync($"/bookings/{bookingId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await users.PostAsJsonAsync("/auth/login", new { login, password = "wrong" })).StatusCode);
        foreach (var client in new[] { users, events, bookings })
        {
            var swagger = await client.GetFromJsonAsync<JsonElement>("/swagger/v1/swagger.json");
            Assert.True(swagger.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
        }
    }

    private static async Task<string> Login(HttpClient client, string login, string password)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new { login, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }
}
