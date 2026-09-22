using AspNetProject.Contracts;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AspNetProject.Events.Infrastructure;

public sealed class KafkaTopicInitializer(IConfiguration config, ILogger<KafkaTopicInitializer> logger) : IHostedService
{
    public async Task<bool> EnsureCreatedAsync(CancellationToken ct)
    {
        using var admin = new AdminClientBuilder(new AdminClientConfig
        {
            BootstrapServers = config["Kafka:BootstrapServers"], SocketTimeoutMs = 5000
        }).Build();
        try
        {
            await admin.CreateTopicsAsync(
                new[] { TopicNames.BookingConfirmed, TopicNames.BookingCancelled }
                    .Select(name => new TopicSpecification { Name = name, NumPartitions = 3, ReplicationFactor = 1 }),
                new CreateTopicsOptions { RequestTimeout = TimeSpan.FromSeconds(5) }).WaitAsync(ct);
            return true;
        }
        catch (CreateTopicsException ex) when (ex.Results.All(r => r.Error.Code == ErrorCode.TopicAlreadyExists || !r.Error.IsError))
        {
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Topic initialization failed; consumer will retry without stopping the API.");
            return false;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken) => await EnsureCreatedAsync(cancellationToken);
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
