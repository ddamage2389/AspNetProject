using System.Text.Json;
using AspNetProject.Bookings.Application;
using AspNetProject.Contracts;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;

namespace AspNetProject.Bookings.Infrastructure;

public sealed class KafkaBookingEventPublisher : IBookingEventPublisher, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public KafkaBookingEventPublisher(IConfiguration configuration)
    {
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 10000,
            AllowAutoCreateTopics = false
        }).Build();
    }

    public Task PublishAsync(BookingConfirmed message, CancellationToken ct) =>
        PublishAsync(TopicNames.BookingConfirmed, message.EventId, message, ct);

    public Task PublishAsync(BookingCancelled message, CancellationToken ct) =>
        PublishAsync(TopicNames.BookingCancelled, message.EventId, message, ct);

    private async Task PublishAsync<T>(string topic, Guid eventId, T message, CancellationToken ct) =>
        await _producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = eventId.ToString("D"), Value = JsonSerializer.Serialize(message)
        }, ct);

    public void Dispose()
    {
        try { _producer.Flush(TimeSpan.FromSeconds(5)); }
        finally { _producer.Dispose(); }
    }
}
