using AspNetProject.Application.Dtos;
using AspNetProject.Application.Interfaces;
using AspNetProject.Application.Services;
using AspNetProject.Application.Settings;
using AspNetProject.Domain.Entities;
using AspNetProject.Infrastructure;
using AspNetProject.Infrastructure.DataAccess;
using AspNetProject.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AspNetProject.Tests;

public class UserServiceTests : IDisposable
{
    private readonly string _dbName;
    private readonly ServiceProvider _serviceProvider;

    public UserServiceTests()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_dbName));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        var jwtSettings = new JwtSettings { Secret = "test_secret_key_for_testing_purposes_only_1234567890", Issuer = "Test", Audience = "Test", ExpiryMinutes = 60 };
        services.AddSingleton<ITokenGenerator>(new JwtTokenGenerator(jwtSettings));
        services.AddSingleton(jwtSettings);

        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeleted();
        _serviceProvider.Dispose();
    }

    private IUserService CreateService() => _serviceProvider.CreateScope().ServiceProvider.GetRequiredService<IUserService>();

    [Fact]
    public async Task RegisterAsync_ShouldCreateUserWithUserRole_AndHashPassword()
    {
        var service = CreateService();
        var dto = new RegisterUserDto { Login = "newuser", Password = "password123" };

        await service.RegisterAsync(dto);

        using var scope = _serviceProvider.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.FirstAsync(u => u.Login == "newuser");

        user.Role.Should().Be(Role.User);
        user.PasswordHash.Should().NotBe("password123");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnToken_WhenCredentialsAreValid()
    {
        var service = CreateService();
        await service.RegisterAsync(new RegisterUserDto { Login = "logintest", Password = "password123" });

        var token = await service.LoginAsync(new LoginUserDto { Login = "logintest", Password = "password123" });

        token.Should().NotBeNullOrEmpty();
        token.Should().StartWith("eyJ"); // Формат JWT
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowUnauthorizedAccessException_WhenPasswordIsInvalid()
    {
        var service = CreateService();
        await service.RegisterAsync(new RegisterUserDto { Login = "user", Password = "password123" });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginUserDto { Login = "user", Password = "wrongpassword" }));
    }

    [Fact]
    public async Task LoginAsync_ShouldThrowUnauthorizedAccessException_WhenUserDoesNotExist()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginUserDto { Login = "nouser", Password = "password123" }));
    }
}