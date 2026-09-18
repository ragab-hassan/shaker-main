using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using HousingManagementWeb.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add<AuditActionFilter>();
});
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
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("HROnly", policy => policy.RequireRole("HR"));
    options.AddPolicy("EmployeeOnly", policy => policy.RequireRole("Employee"));
});


var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await AppSeedData.SeedAsync(db);
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
