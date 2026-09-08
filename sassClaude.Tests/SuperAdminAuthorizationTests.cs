using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using sassClaude.Data;

namespace sassClaude.Tests;

public sealed class SuperAdminAuthorizationTests
{
    [Fact]
    public async Task SuperAdmin_policy_only_allows_the_true_claim_value()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationCore(options =>
            options.AddPolicy("SuperAdmin", policy => policy.RequireClaim(TenantClaimTypes.IsSuperAdmin, "true")));
        var authService = services.BuildServiceProvider().GetRequiredService<IAuthorizationService>();

        var regularUser = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(TenantClaimTypes.IsSuperAdmin, "false") }, "Test"));
        var superAdminUser = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(TenantClaimTypes.IsSuperAdmin, "true") }, "Test"));

        Assert.False((await authService.AuthorizeAsync(regularUser, "SuperAdmin")).Succeeded);
        Assert.True((await authService.AuthorizeAsync(superAdminUser, "SuperAdmin")).Succeeded);
    }
}
