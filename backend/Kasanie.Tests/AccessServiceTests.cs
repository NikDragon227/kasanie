using System.Security.Claims;
using Kasanie.Api.Application;
using Kasanie.Api.Domain;
using Kasanie.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Kasanie.Tests;

public sealed class AccessServiceTests
{
    [Fact]
    public async Task Coach_CannotAccessUnrelatedPlayer()
    {
        await using var db = Database();
        db.CoachProfiles.Add(new CoachProfile { Id = 1, UserId = "coach", DisplayName = "Coach" });
        db.Players.AddRange(Player(1), Player(2));
        db.CoachPlayerLinks.Add(new CoachPlayerLink { CoachId = 1, PlayerId = 1, Status = LinkStatus.Active });
        await db.SaveChangesAsync();
        var access = new AccessService(db);
        Assert.True(await access.CoachCanAccessAsync(User("coach"), 1));
        Assert.False(await access.CoachCanAccessAsync(User("coach"), 2));
    }

    [Fact]
    public async Task Parent_CannotAccessUnrelatedChild()
    {
        await using var db = Database();
        db.ParentProfiles.Add(new ParentProfile { Id = 1, UserId = "parent" });
        db.Players.AddRange(Player(1), Player(2));
        db.ParentPlayerLinks.Add(new ParentPlayerLink { ParentId = 1, PlayerId = 1, Relationship = "Parent", IsPrimary = true, ConsentAccepted = true, ConsentVersion = "v1" });
        await db.SaveChangesAsync();
        var access = new AccessService(db);
        Assert.True(await access.ParentCanAccessAsync(User("parent"), 1));
        Assert.False(await access.ParentCanAccessAsync(User("parent"), 2));
    }

    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static ClaimsPrincipal User(string id) => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id)], "test"));
    private static PlayerProfile Player(int id) => new() { Id = id, FirstName = "P", LastName = id.ToString(), DateOfBirth = new(2010, 1, 1), MunicipalityId = 1, PreferredPosition = "P", DominantFoot = "R", ExperienceLevel = "L" };
}
