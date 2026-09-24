using AspNetProject.Events.Domain.Entities;
using AspNetProject.Events.Infrastructure.Repositories;

namespace AspNetProject.Sprint9.Tests;

[Collection("Sprint9")]
public sealed class TopEventsRepositoryTests(DatabaseFixture fixture)
{
    [Fact]
    public async Task Top_orders_by_fraction_not_absolute_sales_and_limits_to_ten()
    {
        await using var db = fixture.Events();
        // Roll back test data; other integration scenarios use this same fixture.
        await using var tx = await db.Database.BeginTransactionAsync();
        db.Events.RemoveRange(db.Events);
        var items = Enumerable.Range(1, 12).Select(i =>
        {
            var item = Event.Create($"Event {i}", "", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 100 * i);
            item.TryReserveSeats(10 * i * i); // ratios 0.1, 0.2, ...; last two become sold out below.
            if (i > 10) item.TryReserveSeats(item.AvailableSeats);
            return item;
        }).ToList();
        var small = Event.Create("Small sold out", "", DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2), 1);
        small.TryReserveSeats();
        items.Add(small);
        db.Events.AddRange(items);
        await db.SaveChangesAsync();

        var result = await new EventRepository(db).GetTopAsync();

        var expected = items.OrderByDescending(x => (double)(x.TotalSeats - x.AvailableSeats) / x.TotalSeats)
            .ThenBy(x => x.Id).Take(10).Select(x => x.Id);
        Assert.Equal(expected, result.Select(x => x.Id));
        Assert.Equal(10, result.Count);
        Assert.Contains(result, x => x.Id == small.Id);
    }
}
