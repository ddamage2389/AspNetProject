namespace AspNetProject.Events.Application.Caching;

public sealed class CacheSettings
{
    public int EventTtlSeconds { get; set; } = 60;
    public int TopEventsTtlSeconds { get; set; } = 30;
}
