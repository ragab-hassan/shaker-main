using System.Security.Claims;
using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Services;

public class AuditActionFilter : IAsyncActionFilter
{
    private readonly AppDbContext _db;

    public AuditActionFilter(AppDbContext db)
    {
        _db = db;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var resultContext = await next();

        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            return;

        var controllerName = context.Controller?.GetType().Name ?? "Unknown";
        var actionName = context.ActionDescriptor.DisplayName ?? context.RouteData.Values["action"]?.ToString() ?? "Unknown";
        var isPost = context.HttpContext.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase);
        var isWriteAction = isPost && !actionName.Contains("Login", StringComparison.OrdinalIgnoreCase) && !actionName.Contains("Logout", StringComparison.OrdinalIgnoreCase);

        if (!isWriteAction)
            return;

        var userName = context.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ?? "Unknown";
        var httpMethod = context.HttpContext.Request.Method;
        var entityName = controllerName.Replace("Controller", "");
        var path = context.HttpContext.Request.Path.Value ?? string.Empty;

        var details = $"{httpMethod} {path}";

        _db.AuditLogs.Add(new AuditLog
        {
            UserName = userName,
            Action = actionName,
            EntityName = entityName,
            Details = details,
            CreatedAt = DateTime.Now
        });

        await _db.SaveChangesAsync();
    }
}
