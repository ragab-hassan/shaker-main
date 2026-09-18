using HousingManagementWeb.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize(Roles = "Admin")]
public class AuditLogsController : Controller
{
    private readonly AppDbContext _db;

    public AuditLogsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var logs = await _db.AuditLogs
            .OrderByDescending(x => x.CreatedAt)
            .Take(200)
            .ToListAsync();

        return View(logs);
    }
}
