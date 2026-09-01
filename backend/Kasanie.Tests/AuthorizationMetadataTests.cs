using System.Reflection;
using Kasanie.Api.Domain;

namespace Kasanie.Tests;

public sealed class AuthorizationMetadataTests
{
    [Fact]
    public void AllProtectedRoleNamesExistAndAreDistinct()
    {
        Assert.Equal(7, Roles.All.Distinct().Count());
        Assert.Contains(Roles.Admin, Roles.All);
        Assert.Contains(Roles.SchoolOwner, Roles.All);
        Assert.Contains(Roles.SchoolAdmin, Roles.All);
        Assert.Contains(Roles.Organizer, Roles.All);
    }

    [Fact]
    public void RegionalAnalyticsSourceIsNotPartOfTheApi()
    {
        Assert.False(File.Exists(Path.Combine(ProjectRoot(), "Kasanie.Api", "Endpoints", "AnalyticsEndpoints.cs")));
    }

    [Fact]
    public void AdminEndpointsRequireAdminRole()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "Kasanie.Api", "Endpoints", "AdminEndpoints.cs"));
        Assert.Contains("RequireAuthorization(Roles.Admin)", source);
    }

    private static string ProjectRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !Directory.Exists(Path.Combine(current.FullName, "Kasanie.Api"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Backend root not found");
    }
}
