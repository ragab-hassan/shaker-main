using System.Security.Claims;
using System.Text;
using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Services;

public static class AuthHelper
{
    public static string ComputeSha256(string input)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    public static async Task SignInAsync(HttpContext httpContext, AppUser user, AppDbContext db)
    {
        var roles = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.UserName),
            new Claim("FullName", user.FullName),
            new Claim(ClaimTypes.Email, user.Email)
        };

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
    }

    public static async Task<bool> HasPermissionAsync(AppDbContext db, int userId, string permissionName)
    {
        return await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.RolePermissions,
                ur => ur.RoleId,
                rp => rp.RoleId,
                (ur, rp) => rp)
            .Join(db.Permissions,
                rp => rp.PermissionId,
                p => p.Id,
                (rp, p) => p)
            .AnyAsync(p => p.Name == permissionName);
    }
}
