using System.Diagnostics;
using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _db;
    public HomeController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var vm = new DashboardViewModel
        {
            TotalEmployees = await _db.Employees.CountAsync(e => e.IsActive),
            TotalProjects = await _db.Projects.CountAsync(p => p.IsActive),
            TotalVehicles = await _db.Vehicles.CountAsync(v => v.IsActive),
            TotalPassengers = await _db.VehiclePassengers
                .Where(vp => vp.Employee != null && vp.Employee.IsActive)
                .Select(vp => vp.EmployeeId).Distinct().CountAsync(),
            TotalSites = await _db.Sites.CountAsync(),
            TotalBuildings = await _db.Buildings.CountAsync(),
            TotalRooms = await _db.Rooms.CountAsync(),
            OccupiedRooms = await _db.Rooms.CountAsync(r => r.IsOccupied)
        };

        vm.Projects = await _db.Projects
            .Where(p => p.IsActive)
            .Select(p => new ProjectDashboardRow
            {
                Name = p.Name,
                Employees = p.Employees.Count(e => e.IsActive),
                Vehicles = p.Vehicles.Count(v => v.IsActive),
                Passengers = p.Vehicles.SelectMany(v => v.Passengers)
                    .Where(vp => vp.Employee != null && vp.Employee.IsActive)
                    .Select(vp => vp.EmployeeId).Distinct().Count()
            }).OrderByDescending(x => x.Employees).ToListAsync();

        vm.Sites = await _db.Sites
            .Select(s => new SiteDashboardRow
            {
                Name = s.Name,
                Rooms = s.Buildings.SelectMany(b => b.Apartments).SelectMany(a => a.Rooms).Count(),
                Occupied = s.Buildings.SelectMany(b => b.Apartments).SelectMany(a => a.Rooms).Count(r => r.IsOccupied)
            }).OrderBy(s => s.Name).ToListAsync();

        vm.Buildings = await _db.Buildings
            .Include(b => b.Site)
            .Select(b => new BuildingDashboardRow
            {
                SiteName = b.Site != null ? b.Site.Name : "-",
                Number = b.Number,
                Name = b.Name,
                Rooms = b.Apartments.SelectMany(a => a.Rooms).Count(),
                Occupied = b.Apartments.SelectMany(a => a.Rooms).Count(r => r.IsOccupied)
            }).OrderBy(b => b.SiteName).ThenBy(b => b.Number).ToListAsync();

        return View(vm);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View(new ErrorViewModel { RequestId = requestId });
    }
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
