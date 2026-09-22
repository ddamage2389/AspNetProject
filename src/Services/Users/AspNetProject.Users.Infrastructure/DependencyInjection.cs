using AspNetProject.Users.Application.Interfaces;
using AspNetProject.Users.Application.Services;
using AspNetProject.Users.Application.Settings;
using AspNetProject.Users.Domain.Entities;
using AspNetProject.Users.Infrastructure.DataAccess;
using AspNetProject.Users.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetProject.Users.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddUsers(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<UsersDbContext>(o => o.UseNpgsql(config.GetConnectionString("Database")));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton(config.GetSection("JwtSettings").Get<JwtSettings>()!);
        services.AddSingleton<ITokenGenerator, JwtTokenGenerator>();
        return services;
    }

    public static async Task InitializeUsersAsync(this IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
        await db.Database.MigrateAsync();
        var login = config["SeedAdmin:Login"]?.Trim();
        var password = config["SeedAdmin:Password"];
        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password)) return;
        if (!await db.Users.AnyAsync(x => x.Login == login))
        {
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
            db.Users.Add(User.Create(login, hasher.Hash(password), Role.Admin));
            await db.SaveChangesAsync();
        }
    }
}
