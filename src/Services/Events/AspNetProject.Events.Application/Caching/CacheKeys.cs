namespace AspNetProject.Events.Application.Caching;

public static class CacheKeys
{
    public static string Event(Guid id) => $"event:{id:D}";
    public const string TopEvents = "events:top10";
}
