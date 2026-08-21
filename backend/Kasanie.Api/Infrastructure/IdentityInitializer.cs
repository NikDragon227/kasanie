using Kasanie.Api.Domain;
using Microsoft.AspNetCore.Identity;

namespace Kasanie.Api.Infrastructure;

public sealed class IdentityInitializer(
    RoleManager<IdentityRole> roles,
    UserManager<ApplicationUser> users,
    IConfiguration configuration,
    AppDbContext db,
    ILogger<IdentityInitializer> logger)
{
    public async Task InitializeAsync()
    {
        foreach (var roleName in Roles.All)
            if (!await roles.RoleExistsAsync(roleName))
                await roles.CreateAsync(new IdentityRole(roleName));

        var email = configuration["BootstrapAdmin:Email"];
        var password = configuration["BootstrapAdmin:Password"];
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(password)) return;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("BootstrapAdmin:Email and BootstrapAdmin:Password must be provided together.");

        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded) throw new InvalidOperationException("Admin bootstrap failed: " + string.Join("; ", result.Errors.Select(x => x.Description)));
            await users.AddToRoleAsync(admin, Roles.Admin);
            db.AuditLogs.Add(new AuditLog { UserId = admin.Id, EventType = "production_admin_bootstrapped", EntityType = nameof(ApplicationUser), EntityId = admin.Id });
            await db.SaveChangesAsync();
            logger.LogWarning("One-time bootstrap administrator {AdminEmail} was created. Remove BootstrapAdmin credentials from the environment now.", email);
        }
    }
}
