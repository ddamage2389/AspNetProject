using AspNetProject.Application.Dtos;
using AspNetProject.IntegrationTests.Dtos;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace AspNetProject.IntegrationTests;

public class ApiAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ApiAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateEvent_ShouldReturn401_WhenUserIsNotAuthenticated()
    {
        // Arrange
        var client = _factory.CreateClient();

        var request = new CreateEventDto
        {
            Title = "Test event",
            Description = "Desc",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 10
        };

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/events",
            request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateEvent_ShouldReturn403_WhenAuthenticatedUserIsNotAdmin()
    {
        // Arrange
        var client = _factory.CreateClient();

        var login = $"regularuser_{Guid.NewGuid():N}";
        const string password = "password123";

        var registerDto = new RegisterUserDto
        {
            Login = login,
            Password = password
        };

        var loginDto = new LoginUserDto
        {
            Login = login,
            Password = password
        };

        var eventDto = new CreateEventDto
        {
            Title = "Test event",
            Description = "Desc",
            StartAt = DateTime.UtcNow.AddDays(1),
            EndAt = DateTime.UtcNow.AddDays(2),
            TotalSeats = 10
        };

        // Register
        var registerResponse = await client.PostAsJsonAsync(
            "/api/auth/register",
            registerDto);

        registerResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Login
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            loginDto);

        loginResponse.EnsureSuccessStatusCode();

        var loginResult =
            await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        loginResult.Should().NotBeNull();
        loginResult!.Token.Should().NotBeNullOrWhiteSpace();

        // Authenticate as regular User
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult.Token);

        // Act
        var response = await client.PostAsJsonAsync(
            "/api/events",
            eventDto);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}