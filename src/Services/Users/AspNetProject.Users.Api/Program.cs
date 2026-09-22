using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using AspNetProject.Users.Infrastructure;
using AspNetProject.Users.Infrastructure.DataAccess;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Введите JWT без префикса Bearer."
    });
    o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
var jwt = builder.Configuration.GetSection("JwtSettings");
var secret = jwt["Secret"] ?? throw new InvalidOperationException("JwtSettings:Secret is required.");
if (Encoding.UTF8.GetByteCount(secret) < 32)
    throw new InvalidOperationException("JWT secret must contain at least 32 UTF-8 bytes.");
if (string.IsNullOrWhiteSpace(jwt["Issuer"]) || string.IsNullOrWhiteSpace(jwt["Audience"]))
    throw new InvalidOperationException("JWT issuer and audience are required.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
        ValidIssuer = jwt["Issuer"], ValidAudience = jwt["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        RoleClaimType = ClaimTypes.Role, NameClaimType = ClaimTypes.NameIdentifier,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddUsers(builder.Configuration);
var app = builder.Build();
if (builder.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await app.Services.InitializeUsersAsync(builder.Configuration);
}
app.Use(async (context, next) =>
{
    try { await next(context); }
    catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { }
    catch (Exception ex)
    {
        var postgres = ex as PostgresException ?? ex.InnerException as PostgresException;
        var status = ex switch
        {
            UnauthorizedAccessException => 403,
            KeyNotFoundException => 404,
            ArgumentException => 400,
            
            DbUpdateConcurrencyException => 409,
            InvalidOperationException => 409,
            _ when postgres?.SqlState is "23505" or "40001" or "40P01" => 409,
            _ => 500
        };
        if (status == 500) app.Logger.LogError(ex, "Unhandled request error.");
        context.Response.StatusCode = status;
        await Results.Problem(statusCode: status,
            title: status == 500 ? "Internal server error." : ex.Message).ExecuteAsync(context);
    }
});
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", async (UsersDbContext db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(503));
app.Run();

namespace AspNetProject.Users.Api { public sealed class UsersApiMarker { } }
