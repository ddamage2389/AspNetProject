using System.Text.Json;
using AspNetProject.Events.Application.Caching;
using AspNetProject.Events.Application.Dtos;
using AspNetProject.Events.Application.Interfaces;
using AspNetProject.Events.Application.Services;
using AspNetProject.Events.Domain.Entities;

namespace AspNetProject.Sprint10.Tests;

public sealed class EventCacheTests
{
    private readonly List<string> _calls = [];
    private readonly CacheSettings _settings = new() { EventTtlSeconds = 47, TopEventsTtlSeconds = 19 };

    private (EventService Service, RepositoryStub Repository, CacheStub Cache) CreateService()
    {
        var repository = new RepositoryStub(_calls);
        var cache = new CacheStub(_calls);
        return (new EventService(repository, cache, _settings), repository, cache);
    }

    private static Event CreateEvent() => Event.Create("Concert", "Description", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100);

    [Fact]
    public async Task Event_cache_hit_does_not_call_repository()
    {
        var (service, repository, cache) = CreateService();
        var item = EventDetails.From(CreateEvent());
        cache.Values[CacheKeys.Event(item.Id)] = item;
        Assert.Equal(item, await service.GetByIdAsync(item.Id));
        Assert.Equal(0, repository.Reads);
        Assert.Empty(cache.Writes);
    }

    [Fact]
    public async Task Event_cache_miss_loads_database_and_caches_all_fields_with_configured_TTL()
    {
        var (service, repository, cache) = CreateService();
        var item = CreateEvent();
        item.TryReserveSeats(17);
        repository.Item = item;
        var result = await service.GetByIdAsync(item.Id);
        Assert.Equal(EventDetails.From(item), result);
        Assert.Equal(83, result!.AvailableSeats);
        Assert.Equal(1, repository.Reads);
        var write = Assert.Single(cache.Writes);
        Assert.Equal(CacheKeys.Event(item.Id), write.Key);
        Assert.Equal(TimeSpan.FromSeconds(47), write.Ttl);
        Assert.Equal(result, cache.Values[write.Key]);
        await service.GetByIdAsync(item.Id);
        Assert.Equal(1, repository.Reads);
    }

    [Fact]
    public async Task Missing_event_is_not_cached()
    {
        var (service, repository, cache) = CreateService();
        Assert.Null(await service.GetByIdAsync(Guid.NewGuid()));
        Assert.Equal(1, repository.Reads);
        Assert.Empty(cache.Writes);
    }

    [Fact]
    public async Task Top_cache_hit_does_not_call_repository()
    {
        var (service, repository, cache) = CreateService();
        var items = new[] { EventDetails.From(CreateEvent()) };
        cache.Values[CacheKeys.TopEvents] = items;
        Assert.Equal(items, await service.GetTopAsync());
        Assert.Equal(0, repository.Reads);
        Assert.Empty(cache.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Top_cache_miss_caches_result_including_empty_list(bool empty)
    {
        var (service, repository, cache) = CreateService();
        repository.Top = empty ? [] : [CreateEvent()];
        var result = await service.GetTopAsync();
        Assert.Equal(repository.Top.Select(EventDetails.From), result);
        var write = Assert.Single(cache.Writes);
        Assert.Equal(CacheKeys.TopEvents, write.Key);
        Assert.Equal(TimeSpan.FromSeconds(19), write.Ttl);
        await service.GetTopAsync();
        Assert.Equal(1, repository.Reads);
        Assert.Single(cache.Writes);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Mutation_invalidates_only_event_after_database_save(string operation)
    {
        var (service, repository, cache) = CreateService();
        var item = CreateEvent();
        repository.Item = item;
        cache.Values[CacheKeys.Event(item.Id)] = EventDetails.From(item);
        cache.Values[CacheKeys.TopEvents] = new[] { EventDetails.From(item) };

        await Mutate(service, operation, item);

        Assert.Equal(new[] { CacheKeys.Event(item.Id) }, cache.Removed);
        Assert.True(_calls.IndexOf("save") < _calls.IndexOf("remove"));
        Assert.False(cache.Values.ContainsKey(CacheKeys.Event(item.Id)));
        Assert.True(cache.Values.ContainsKey(CacheKeys.TopEvents));
        Assert.Empty(cache.Writes);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Failed_database_save_does_not_invalidate_cache(string operation)
    {
        var (service, repository, cache) = CreateService();
        var item = CreateEvent();
        repository.Item = item;
        repository.FailSave = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Mutate(service, operation, item));
        Assert.Empty(cache.Removed);
        Assert.Empty(cache.Writes);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task Mutation_of_missing_event_does_not_touch_cache(string operation)
    {
        var (service, _, cache) = CreateService();
        var item = CreateEvent();
        if (operation == "update") Assert.Null(await service.UpdateAsync(item.Id, Update(item)));
        else Assert.False(await service.DeleteAsync(item.Id));
        Assert.Empty(cache.Removed);
        Assert.DoesNotContain("save", _calls);
    }

    [Fact]
    public async Task Update_reads_database_even_if_cached_value_exists_and_next_read_refreshes_it()
    {
        var (service, repository, cache) = CreateService();
        var item = CreateEvent();
        repository.Item = item;
        cache.Values[CacheKeys.Event(item.Id)] = EventDetails.From(item) with { Title = "Stale" };
        await service.UpdateAsync(item.Id, Update(item));
        Assert.Equal(1, repository.Reads);
        Assert.Equal("Updated", (await service.GetByIdAsync(item.Id))!.Title);
        Assert.Equal(2, repository.Reads);
    }

    [Fact]
    public async Task Unavailable_cache_contract_falls_back_to_database_for_reads_and_writes()
    {
        var (service, repository, cache) = CreateService();
        cache.Unavailable = true;
        var item = CreateEvent();
        repository.Item = item;
        repository.Top = [item];
        Assert.Equal(item.Id, (await service.GetByIdAsync(item.Id))!.Id);
        Assert.Single(await service.GetTopAsync());
        Assert.Equal("Updated", (await service.UpdateAsync(item.Id, Update(item)))!.Title);
        Assert.True(await service.DeleteAsync(item.Id));
    }

    [Fact]
    public void Cached_read_model_round_trips_without_losing_seats_or_dates()
    {
        var item = CreateEvent();
        item.TryReserveSeats(7);
        var expected = EventDetails.From(item);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        Assert.Equal(expected, JsonSerializer.Deserialize<EventDetails>(JsonSerializer.Serialize(expected, options), options));
    }

    private static UpdateEventDto Update(Event item) => new()
    {
        Title = "Updated", Description = "New", StartAt = item.StartAt, EndAt = item.EndAt
    };

    private static async Task Mutate(EventService service, string operation, Event item)
    {
        switch (operation)
        {
            case "create": await service.CreateAsync(item); break;
            case "update": await service.UpdateAsync(item.Id, Update(item)); break;
            case "delete": await service.DeleteAsync(item.Id); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private sealed class CacheStub(List<string> calls) : ICache
    {
        public Dictionary<string, object> Values { get; } = [];
        public List<(string Key, TimeSpan Ttl)> Writes { get; } = [];
        public List<string> Removed { get; } = [];
        public bool Unavailable { get; set; }

        public Task<T?> GetAsync<T>(string key) where T : class =>
            Task.FromResult(!Unavailable && Values.TryGetValue(key, out var value) ? (T)value : null);
        public Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
        {
            if (!Unavailable) { Values[key] = value; Writes.Add((key, ttl)); }
            return Task.CompletedTask;
        }
        public Task RemoveAsync(string key)
        {
            calls.Add("remove");
            Removed.Add(key);
            if (!Unavailable) Values.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class RepositoryStub(List<string> calls) : IEventRepository
    {
        public Event? Item { get; set; }
        public IReadOnlyList<Event> Top { get; set; } = [];
        public int Reads { get; private set; }
        public bool FailSave { get; set; }
        public Task<Event?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(Item?.Id == id ? Item : null);
        }
        public Task<IReadOnlyList<Event>> GetTopAsync(CancellationToken cancellationToken = default)
        {
            Reads++;
            return Task.FromResult(Top);
        }
        public Task<PaginatedResult<Event>> GetAllAsync(string? title, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddAsync(Event item, CancellationToken cancellationToken = default) { Item = item; return Task.CompletedTask; }
        public Task UpdateAsync(Event item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) { Item = null; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            calls.Add("save");
            return FailSave ? Task.FromException(new InvalidOperationException("Database write failed")) : Task.CompletedTask;
        }
    }
}
