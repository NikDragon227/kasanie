using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Tests;

public sealed class PlatformCatalogSeederTests
{
    [Fact]
    public async Task SeedAsync_AddsBaselineCatalogAndIsIdempotent()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var seeder = new PlatformCatalogSeeder(db);

        await seeder.SeedAsync();
        await seeder.SeedAsync();

        Assert.Equal(12, await db.Exercises.CountAsync());
        Assert.Equal(12, await db.Exercises.Select(x => x.Name).Distinct().CountAsync());
        Assert.All(await db.Exercises.ToListAsync(), x => Assert.True(x.IsActive));
    }
}
