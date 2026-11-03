using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Services;

public static class AppSeedData
{
    public static async Task SeedAsync(AppDbContext db, string? bootstrapAdminPassword)
    {
        if (!string.IsNullOrWhiteSpace(bootstrapAdminPassword) && bootstrapAdminPassword.Length < 12)
        {
            throw new InvalidOperationException("BootstrapAdmin:Password must be at least 12 characters.");
        }

        var roles = new[]
        {
            new AppRole { Name = "Admin", Description = "مدير النظام", IsSystemRole = true },
            new AppRole { Name = "Manager", Description = "مدير", IsSystemRole = true },
            new AppRole { Name = "HR", Description = "الموارد البشرية", IsSystemRole = true },
            new AppRole { Name = "Employee", Description = "موظف", IsSystemRole = true }
        };

        foreach (var role in roles)
        {
            if (!await db.AppRoles.AnyAsync(existing => existing.Name == role.Name))
            {
                db.AppRoles.Add(role);
            }
        }
        await db.SaveChangesAsync();

        var permissions = new[]
        {
            new Permission { Name = AppPermissions.ViewDashboard, Description = "عرض لوحة التحكم" },
            new Permission { Name = AppPermissions.ManageEmployees, Description = "إدارة الموظفين" },
            new Permission { Name = AppPermissions.ManageProjects, Description = "إدارة المشاريع" },
            new Permission { Name = AppPermissions.ManageLocations, Description = "إدارة المواقع والسكن" },
            new Permission { Name = AppPermissions.ManageVehicles, Description = "إدارة العربات والنقل" },
            new Permission { Name = AppPermissions.ManageHousingAssignments, Description = "إدارة التسكين المنتظر" },
            new Permission { Name = AppPermissions.ManageUsers, Description = "إدارة المستخدمين" },
            new Permission { Name = AppPermissions.ManageRoles, Description = "إدارة الأدوار والصلاحيات" }
        };

        foreach (var permission in permissions)
        {
            if (!await db.Permissions.AnyAsync(existing => existing.Name == permission.Name))
            {
                db.Permissions.Add(permission);
            }
        }
        await db.SaveChangesAsync();

        var permissionsByName = await db.Permissions.ToDictionaryAsync(permission => permission.Name);
        var rolePermissions = new Dictionary<string, string[]>
        {
            ["Admin"] = permissions.Select(permission => permission.Name).ToArray(),
            ["Manager"] = [AppPermissions.ViewDashboard],
            ["HR"] = [AppPermissions.ViewDashboard, AppPermissions.ManageHousingAssignments],
            ["Employee"] = [AppPermissions.ViewDashboard]
        };

        foreach (var role in await db.AppRoles.ToListAsync())
        {
            if (!rolePermissions.TryGetValue(role.Name, out var permissionNames)) continue;

            foreach (var permissionName in permissionNames)
            {
                var permission = permissionsByName[permissionName];
                var exists = await db.RolePermissions.AnyAsync(rolePermission =>
                    rolePermission.RoleId == role.Id && rolePermission.PermissionId == permission.Id);
                if (!exists)
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                }
            }
        }
        await db.SaveChangesAsync();

        var adminRole = await db.AppRoles.SingleAsync(role => role.Name == "Admin");
        var adminUser = await db.AppUsers.FirstOrDefaultAsync(user => user.UserName == "admin");
        var ensureBootstrapRole = false;
        if (adminUser != null && BCrypt.Net.BCrypt.Verify("", adminUser.PasswordHash))
        {
            ensureBootstrapRole = true;
            if (string.IsNullOrWhiteSpace(bootstrapAdminPassword))
            {
                adminUser.IsActive = false;
            }
            else
            {
                adminUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(bootstrapAdminPassword);
                adminUser.IsActive = true;
            }
            await db.SaveChangesAsync();
        }

        if (adminUser == null && !string.IsNullOrWhiteSpace(bootstrapAdminPassword))
        {
            var adminEmail = "admin@housing.local";
            if (await db.AppUsers.AnyAsync(user => user.Email == adminEmail))
            {
                adminEmail = $"admin-bootstrap-{Guid.NewGuid():N}@housing.local";
            }
            adminUser = new AppUser
            {
                UserName = "admin",
                FullName = "مدير النظام",
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(bootstrapAdminPassword),
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            db.AppUsers.Add(adminUser);
            await db.SaveChangesAsync();
            ensureBootstrapRole = true;
        }

        if (ensureBootstrapRole && adminUser != null && !await db.UserRoles.AnyAsync(userRole =>
                userRole.UserId == adminUser.Id && userRole.RoleId == adminRole.Id))
        {
            db.UserRoles.Add(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id });
            await db.SaveChangesAsync();
        }
    }
}
