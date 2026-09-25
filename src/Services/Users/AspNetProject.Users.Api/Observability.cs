using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;

namespace AspNetProject.Users.Api;

public static class Observability
{
    public static void AddObservability(this WebApplicationBuilder builder)
    {
        var serviceName = builder.Configuration["Observability:ServiceName"]
            ?? throw new InvalidOperationException("Observability:ServiceName is required.");
        var endpoint = new Uri(builder.Configuration["Otlp:Endpoint"]
            ?? throw new InvalidOperationException("Otlp:Endpoint is required."));

        builder.Host.UseSerilog((context, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("ServiceName", serviceName)
            .WriteTo.Console(new CompactJsonFormatter()), preserveStaticLogger: true);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation(options =>
                    options.Filter = context => !context.Request.Path.StartsWithSegments("/metrics"))
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation();
                if (builder.Configuration.GetValue("Otlp:Enabled", true))
                    tracing.AddOtlpExporter(options =>
                    {
                        options.Endpoint = endpoint;
                        options.Protocol = OtlpExportProtocol.Grpc;
                    });
            })
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter());
    }

    public static void UseRequestLogging(this WebApplication app)
    {
        app.UseSerilogRequestLogging(options =>
        {
            // Each host owns its logger; the static Log.Logger stays untouched.
            options.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
            // Prometheus scrapes should not dominate application request logs.
            options.GetLevel = (context, _, exception) =>
                exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error :
                context.Request.Path.StartsWithSegments("/metrics") ? LogEventLevel.Debug :
                context.Response.StatusCode >= 400 ? LogEventLevel.Warning : LogEventLevel.Information;
            options.EnrichDiagnosticContext = (diagnostics, context) =>
                diagnostics.Set("RequestId", context.TraceIdentifier);
        });
    }
}
