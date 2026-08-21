using System.Reflection;
using Kasanie.Api.Domain;

namespace Kasanie.Tests;

public sealed class AuthorizationMetadataTests
{
    [Fact]
    public void AllProtectedRoleNamesExistAndAreDistinct()
    {
        Assert.Equal(5, Roles.All.Distinct().Count());
        Assert.Contains(Roles.Admin, Roles.All);
        Assert.Contains(Roles.RegionalAnalyst, Roles.All);
    }

    [Fact]
    public void RegionalContractDoesNotContainPersonalFieldNames()
    {
        var source = File.ReadAllText(Path.Combine(ProjectRoot(), "Kasanie.Api", "Endpoints", "AnalyticsEndpoints.cs"));
        var responseBlock = source[source.IndexOf("return Results.Ok", StringComparison.Ordinal)..];
        Assert.DoesNotContain("FirstName", responseBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LastName", responseBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("email =", responseBlock, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".DateOfBirth", responseBlock, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("RequireAuthorization(Roles.RegionalAnalyst)", source);
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
