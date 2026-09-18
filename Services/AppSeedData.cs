using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Services;

public static class AppSeedData
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.AppRoles.AnyAsync())
        {
            var adminRole = new AppRole { Name = "Admin", Description = "مدير النظام", IsSystemRole = true };
            var managerRole = new AppRole { Name = "Manager", Description = "مدير", IsSystemRole = true };
            var employeeRole = new AppRole { Name = "Employee", Description = "موظف", IsSystemRole = true };
            db.AppRoles.AddRange(adminRole, managerRole, employeeRole);
            await db.SaveChangesAsync();
        }

        var roles = await db.AppRoles.ToListAsync();
        var adminRoleEntity = roles.FirstOrDefault(r => r.Name == "Admin");
        var managerRoleEntity = roles.FirstOrDefault(r => r.Name == "Manager");
        var employeeRoleEntity = roles.FirstOrDefault(r => r.Name == "Employee");

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
            if (!await db.Permissions.AnyAsync(p => p.Name == permission.Name))
            {
                db.Permissions.Add(permission);
            }
        }
        await db.SaveChangesAsync();

        var permissionList = await db.Permissions.ToListAsync();

        foreach (var role in new[] { adminRoleEntity, managerRoleEntity, employeeRoleEntity })
        {
            if (role == null) continue;

            var rolePermissionNames = role.Name switch
            {
                "Admin" => new[]
                {
                    AppPermissions.ViewDashboard,
                    AppPermissions.ManageEmployees,
                    AppPermissions.ManageProjects,
                    AppPermissions.ManageLocations,
                    AppPermissions.ManageVehicles,
                    AppPermissions.ManageHousingAssignments,
                    AppPermissions.ManageUsers,
                    AppPermissions.ManageRoles
                },
                "Manager" => new[]
                {
                    AppPermissions.ViewDashboard,
                    
                },
                "HR" => new[]
                {
                    AppPermissions.ViewDashboard
                    AppPermissions.ManageHousingAssignments,
                },
                "Employee" => new[]
                {
                    AppPermissions.ViewDashboard
                    AppPermissions.ManageEmployees,
                },
                _ => Array.Empty<string>()
            };

            foreach (var permissionName in rolePermissionNames)
            {
                var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
                if (permission == null) continue;

                var exists = await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id);
                if (!exists)
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                }
            }
        }

        await db.SaveChangesAsync();

        if (!await db.AppUsers.AnyAsync())
        {
            var adminUser = new AppUser
            {
                UserName = "admin",
                FullName = "مدير النظام",
                Email = "admin@housing.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(""),
                IsActive = true,
                CreatedAt = DateTime.Now
            };
            db.AppUsers.Add(adminUser);
            await db.SaveChangesAsync();

            var adminUserRole = new UserRole
            {
                UserId = adminUser.Id,
                RoleId = adminRoleEntity?.Id ?? 1
            };
            db.UserRoles.Add(adminUserRole);
            await db.SaveChangesAsync();
        }
    }
}
