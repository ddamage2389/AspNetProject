using AspNetProject.Application.Interfaces;
using AspNetProject.Application.Services;
using AspNetProject.Infrastructure;
using AspNetProject.Infrastructure.DataAccess;
using AspNetProject.Presentation.Middleware;
using Microsoft.EntityFrameworkCore;
using AspNetProject.Presentation.Services;
using AspNetProject.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

Console.WriteLine(
    $"ContentRoot: {builder.Environment.ContentRootPath}");

Console.WriteLine(
    $"Connection string loaded: " +
    $"{!string.IsNullOrWhiteSpace(
        builder.Configuration.GetConnectionString("DefaultConnection"))}");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"Connection string loaded: {!string.IsNullOrWhiteSpace(connectionString)}");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();

builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IBookingService, BookingService>();

builder.Services.AddHostedService<BookingProcessingBackgroundService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.UseExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();