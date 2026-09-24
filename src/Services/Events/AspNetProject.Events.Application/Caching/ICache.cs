namespace AspNetProject.Events.Application.Caching;

// A cache miss or cache failure returns null. Storage failures are logged by the implementation.
public interface ICache
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class;
    Task RemoveAsync(string key);
}
