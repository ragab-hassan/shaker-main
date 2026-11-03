using System.Security.Claims;
using HousingManagementWeb.Data;
using Microsoft.AspNetCore.Authorization;

namespace HousingManagementWeb.Services;

public sealed record PermissionRequirement(string PermissionName) : IAuthorizationRequirement;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly AppDbContext _db;

    public PermissionAuthorizationHandler(AppDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
            return;
        }

        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var userId)
            && await AuthHelper.HasPermissionAsync(_db, userId, requirement.PermissionName))
        {
            context.Succeed(requirement);
        }
    }
}
