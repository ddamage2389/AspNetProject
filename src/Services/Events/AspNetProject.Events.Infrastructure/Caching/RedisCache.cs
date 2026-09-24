using System.Text.Json;
using AspNetProject.Events.Application.Caching;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AspNetProject.Events.Infrastructure.Caching;

public sealed class RedisCache(IConnectionMultiplexer connection, ILogger<RedisCache> logger) : ICache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        try
        {
            var value = await connection.GetDatabase().StringGetAsync(key);
            return value.IsNull ? null : JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogWarning(ex, "Cache read failed for {CacheKey}; using the database.", key);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl) where T : class
    {
        try
        {
            await connection.GetDatabase().StringSetAsync(key, JsonSerializer.Serialize(value, JsonOptions), ttl);
        }
        catch (Exception ex) when (ex is RedisException or JsonException)
        {
            logger.LogWarning(ex, "Cache write failed for {CacheKey}; database data is unaffected.", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await connection.GetDatabase().KeyDeleteAsync(key);
        }
        catch (RedisException ex)
        {
            logger.LogWarning(ex, "Cache invalidation failed for {CacheKey}; the cached value will expire by TTL.", key);
        }
    }
}
