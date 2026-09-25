using System.Collections.Concurrent;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using AspNetProject.Bookings.Api;
using AspNetProject.Events.Api;
using AspNetProject.Users.Api;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenTelemetry;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace AspNetProject.Sprint9.Tests;

[Collection("Sprint9")]
public sealed class ObservabilityTests(DatabaseFixture fixture)
{
    [Theory]
    [InlineData("users")]
    [InlineData("events")]
    [InlineData("bookings")]
    public Task Every_API_exposes_metrics_traces_and_correlated_request_logs(string service) => service switch
    {
        "users" => Verify<UsersApiMarker>(service),
        "events" => Verify<EventsApiMarker>(service),
        _ => Verify<BookingsApiMarker>(service)
    };

    private async Task Verify<T>(string service) where T : class
    {
        var spans = new ConcurrentBag<Activity>();
        var logs = new ConcurrentBag<LogEvent>();
        using var logger = new LoggerConfiguration().WriteTo.Sink(new CaptureSink(logs)).CreateLogger();
        using var factory = new ServiceFactory<T>(fixture.Connection(service));
        using var observed = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<Serilog.ILogger>(logger);
            services.ConfigureOpenTelemetryTracerProvider((_, provider) => provider
                .AddProcessor(new SimpleActivityExportProcessor(new CaptureExporter(spans))));
        }));
        using var client = observed.CreateClient();

        if (service == "users")
        {
            var login = await client.PostAsJsonAsync("/auth/login", new { login = "missing-user", password = "invalid-password" });
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }
        else if (service == "events")
        {
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/events")).StatusCode);
        }
        else
        {
            var token = new JwtSecurityToken("sprint9-tests", "sprint9-tests",
                [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Role, "User")],
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes("sprint9-tests-shared-jwt-secret-at-least-32-bytes")),
                    SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/bookings/{Guid.NewGuid()}")).StatusCode);
            client.DefaultRequestHeaders.Authorization = null;
        }

        Assert.Contains(spans, span => span.Kind == ActivityKind.Server);
        var sqlSpans = spans.Where(span => span.Kind == ActivityKind.Client &&
            span.Source.Name.Contains("EntityFrameworkCore", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(sqlSpans);
        Assert.All(sqlSpans, sql => Assert.Contains(spans, parent => parent.Kind == ActivityKind.Server &&
            parent.TraceId == sql.TraceId && parent.SpanId == sql.ParentSpanId));
        var requestLog = Assert.Single(logs, log => log.Properties.ContainsKey("RequestMethod"));
        Assert.Equal(service == "events" ? LogEventLevel.Information : LogEventLevel.Warning, requestLog.Level);
        Assert.Contains(spans, span => span.Kind == ActivityKind.Server &&
            span.TraceId == requestLog.TraceId && span.SpanId == requestLog.SpanId);
        Assert.True(requestLog.Properties.ContainsKey("RequestId"));

        var metrics = await client.GetAsync("/metrics");
        Assert.Equal(HttpStatusCode.OK, metrics.StatusCode);
        var text = await metrics.Content.ReadAsStringAsync();
        Assert.Contains("target_info", text);
        Assert.Contains($"service_name=\"{service}-service\"", text);
        Assert.Contains("http_server_request_duration_seconds_bucket", text);
        Assert.Contains("http_server_request_duration_seconds_count", text);
        Assert.Contains("http_server_active_requests", text);
        // Routing diagnostics may mention /metrics; it must not enter request duration/RPS.
        await Task.Delay(350); // The exporter caches scrape responses for 300 ms.
        var nextScrape = await client.GetStringAsync("/metrics");
        Assert.DoesNotContain(nextScrape.Split('\n').Where(line => line.StartsWith("http_server_request_duration_")),
            line => line.Contains("http_route=\"/metrics\""));
        Assert.DoesNotContain(spans, span => span.GetTagItem("http.route") as string == "/metrics");
        Assert.DoesNotContain(logs, log => log.Properties.TryGetValue("RequestPath", out var path) &&
            path is ScalarValue { Value: "/metrics" });
    }

    private sealed class CaptureSink(ConcurrentBag<LogEvent> logs) : ILogEventSink
    {
        public void Emit(LogEvent logEvent) => logs.Add(logEvent);
    }

    private sealed class CaptureExporter(ConcurrentBag<Activity> spans) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var span in batch) spans.Add(span);
            return ExportResult.Success;
        }
    }
}
