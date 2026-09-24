using AspNetProject.Events.Application.Caching;

namespace AspNetProject.Sprint9.Tests;

// Keeps integration tests isolated from any Redis running on the developer's machine.
public sealed class TestCache : ICache
{
    public List<string> Removed { get; } = [];
    public Func<string, Task>? OnRemove { get; set; }
    public Task<T?> GetAsync<T>(string key) where T : class => Task.FromResult<T?>(null);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class => Task.CompletedTask;
    public async Task RemoveAsync(string key)
    {
        Removed.Add(key);
        if (OnRemove is not null) await OnRemove(key);
    }
}
