using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using HousingManagementWeb.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AuditActionFilter>();
});
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<EmployeeTransportAssignmentService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdClaim = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out var userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
            var user = await db.AppUsers
                .Where(appUser => appUser.Id == userId && appUser.IsActive)
                .Select(appUser => new
                {
                    Roles = appUser.UserRoles.Select(userRole => userRole.Role.Name).ToList()
                })
                .FirstOrDefaultAsync();
            var currentRoles = context.Principal!.FindAll(ClaimTypes.Role)
                .Select(claim => claim.Value)
                .ToHashSet(StringComparer.Ordinal);
            if (user == null || !currentRoles.SetEquals(user.Roles))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("HROnly", policy => policy.RequireRole("HR"));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole("Employee"));
    foreach (var permissionName in new[]
    {
        AppPermissions.ManageEmployees,
        AppPermissions.ManageProjects,
        AppPermissions.ManageLocations,
        AppPermissions.ManageVehicles,
        AppPermissions.ManageHousingAssignments
    })
    {
        options.AddPolicy(permissionName, policy =>
            policy.AddRequirements(new PermissionRequirement(permissionName)));
    }
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AppSeedData.SeedAsync(db, builder.Configuration["BootstrapAdmin:Password"]);
    if (!await db.AppUsers.AnyAsync(user => user.UserName == "admin" && user.IsActive))
    {
        app.Logger.LogWarning("No active admin account exists. Set BootstrapAdmin__Password (12+ characters) to provision one.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    var isAuthPath = path.StartsWith("/Account", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/Home/Error", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/favicon.ico", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/css ", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/js", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/lib", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/images", StringComparison.OrdinalIgnoreCase);

    if (!isAuthPath && !context.User.Identity?.IsAuthenticated == true)
    {
        var returnUrl = Uri.EscapeDataString(context.Request.Path + context.Request.QueryString.Value);
        context.Response.Redirect($"/Account/Login?returnUrl={returnUrl}");
        return;
    }

    await next();
});

app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
