using System;
using System.Linq;
using System.Threading.Tasks;
using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HousingManagementWeb.Controllers;

[Authorize(Policy = AppPermissions.ManageVehicles)]
public class VehiclesController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<VehiclesController> _logger;

    public VehiclesController(AppDbContext db, ILogger<VehiclesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        await LoadLists();
        var vehicles = await _db.Vehicles
            .Include(v => v.Project)
            .Include(v => v.DriverEmployee)
            .OrderBy(v => v.ProjectName)
            .ThenBy(v => v.PlateNumber)
            .ToListAsync();
        return View(vehicles);
    }

    private async Task LoadLists()
    {
        ViewBag.Projects = await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Drivers = await _db.Employees.Where(e => e.IsActive).OrderBy(e => e.FullName).ToListAsync();
    }

    // kept for direct navigation if needed
    public async Task<IActionResult> Create() { await LoadLists(); return View(new Vehicle { VehicleType = "هاي اس", Capacity = 14, IsActive = true }); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Vehicle model)
    {
        PrepareVehicle(model);
        ValidateVehicle(model);

        if (!ModelState.IsValid)
        {
            await LoadLists();
            // If AJAX request, return validation errors as JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var errors = ModelState
                    .Where(kv => kv.Value != null && kv.Value.Errors.Count > 0)
                    .ToDictionary(
                        kv => kv.Key.Replace("model.", "").Replace("Vehicle.", "").Replace("Vehicle", "").Trim('.'),
                        kv => kv.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? []
                    );
                return BadRequest(new { success = false, errors });
            }
            return View(model);
        }

        try
        {
            await ApplyRelations(model);
            _db.Vehicles.Add(model);
            await _db.SaveChangesAsync();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                var v = model;
                return Json(new
                {
                    success = true,
                    vehicle = new
                    {
                        id = v.Id,
                        plateNumber = v.PlateNumber,
                        vehicleCode = v.VehicleCode,
                        vehicleNumber = v.VehicleNumber,
                        vehicleType = v.VehicleType,
                        projectName = v.ProjectName,
                        driverName = v.DriverName,
                        capacity = v.Capacity,
                        route = v.Route,
                        isActive = v.IsActive
                    }
                });
            }

            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create a vehicle.");
            ModelState.AddModelError(string.Empty, "تعذر حفظ السيارة. حاول مرة أخرى.");
            await LoadLists();
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return BadRequest(new { success = false, errors = new { _global = new[] { "تعذر حفظ السيارة. حاول مرة أخرى." } } });
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(int id) { var x = await _db.Vehicles.FindAsync(id); if (x == null) return NotFound(); await LoadLists(); return View(x); }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Vehicle model)
    {
        if (id != model.Id) return NotFound();
        PrepareVehicle(model);
        ValidateVehicle(model);
        if (!ModelState.IsValid) { await LoadLists(); return View(model); }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        await ValidateVehiclePassengerCapacity(model);
        if (!ModelState.IsValid) { await LoadLists(); return View(model); }

        await ApplyRelations(model);
        _db.Update(model); await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id) { var x = await _db.Vehicles.FindAsync(id); if (x != null) { _db.Vehicles.Remove(x); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }

    private void PrepareVehicle(Vehicle model)
    {
        model.VehicleCode = model.VehicleCode?.Trim() ?? "";
        model.VehicleNumber = model.VehicleNumber?.Trim() ?? "";
        model.PlateNumber = $"{model.VehicleCode}-{model.VehicleNumber}";
        model.Capacity = model.VehicleType switch
        {
            "ملاكي" => 4,
            "هاي اس" => 14,
            _ => 0
        };
    }

    private void ValidateVehicle(Vehicle model)
    {
        if (string.IsNullOrWhiteSpace(model.VehicleCode)) ModelState.AddModelError(nameof(model.VehicleCode), "كود السيارة مطلوب");
        if (string.IsNullOrWhiteSpace(model.VehicleNumber)) ModelState.AddModelError(nameof(model.VehicleNumber), "رقم السيارة مطلوب");
        if (!model.ProjectId.HasValue) ModelState.AddModelError(nameof(model.ProjectId), "اختيار المشروع مطلوب");
        if (model.VehicleType is not ("ملاكي" or "هاي اس")) ModelState.AddModelError(nameof(model.VehicleType), "اختر نوع السيارة");
    }

    private async Task ValidateVehiclePassengerCapacity(Vehicle model)
    {
        var existingVehicle = await _db.Vehicles
            .AsNoTracking()
            .Include(vehicle => vehicle.Passengers)
            .FirstOrDefaultAsync(vehicle => vehicle.Id == model.Id);
        if (existingVehicle == null) return;

        var seatHolders = existingVehicle.Passengers
            .Select(passenger => passenger.EmployeeId)
            .ToHashSet();
        if (model.DriverEmployeeId.HasValue)
        {
            seatHolders.Add(model.DriverEmployeeId.Value);
        }

        if (seatHolders.Count > model.Capacity)
        {
            ModelState.AddModelError(
                nameof(model.VehicleType),
                $"لا يمكن تغيير نوع السيارة؛ بها {seatHolders.Count} مقاعد محجوزة والسعة الجديدة {model.Capacity}.");
        }
    }

    private async Task ApplyRelations(Vehicle model)
    {
        model.ProjectName = model.ProjectId.HasValue ? (await _db.Projects.FindAsync(model.ProjectId.Value))?.Name ?? "" : "";
        model.DriverName = model.DriverEmployeeId.HasValue ? (await _db.Employees.FindAsync(model.DriverEmployeeId.Value))?.FullName ?? "" : "";
    }
}