using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using HousingManagementWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize(Policy = AppPermissions.ManageEmployees)]
public class EmployeesController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmployeeTransportAssignmentService _transportAssignmentService;

    public EmployeesController(
        AppDbContext db,
        EmployeeTransportAssignmentService transportAssignmentService)
    {
        _db = db;
        _transportAssignmentService = transportAssignmentService;
    }

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.Employees.Include(e => e.Project).Include(e => e.Room).ThenInclude(r => r!.Apartment).ThenInclude(a => a!.Building).ThenInclude(b => b!.Site).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(e => e.FullName.Contains(q) || e.NationalId.Contains(q) || e.Phone.Contains(q) || e.ProjectName.Contains(q));
        ViewBag.Q = q;
        return View(await query.OrderByDescending(e => e.Id).ToListAsync());
    }

    private async Task LoadLists(int? selectedRoomId = null, int? selectedSiteId = null)
    {
        ViewBag.Projects = await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Sites = await _db.Sites.OrderBy(s => s.Name).ToListAsync();
        ViewBag.SelectedSiteId = selectedSiteId;
        ViewBag.Rooms = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .ThenInclude(b => b!.Site)
            .Where(r => r.Id == selectedRoomId
                || (!r.IsOccupied
                    && !_db.Employees.Any(e =>
                        e.RoomId == r.Id && e.IsActive && e.ResignationDate == null)
                    && !_db.PendingHousingAssignments.Any(a =>
                        a.RoomId == r.Id && !a.IsApplied)))
            .OrderBy(r => r.Apartment!.Building!.Site!.Name)
            .ThenBy(r => r.Apartment!.Building!.Number)
            .ThenBy(r => r.Apartment!.Number)
            .ThenBy(r => r.Number)
            .ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        await LoadLists();
        return View(new Employee { HousingDate = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee model, int? siteId)
    {
        if (string.IsNullOrWhiteSpace(model.EmployeeCode)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف مطلوب");
        else if (await _db.Employees.AnyAsync(e => e.EmployeeCode == model.EmployeeCode)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف موجود بالفعل");

        await ValidateRoomSelection(model.RoomId, siteId, null, null);
        if (!ModelState.IsValid) { await LoadLists(model.RoomId, siteId); return View(model); }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        await ValidateRoomSelection(model.RoomId, siteId, null, null);
        if (!ModelState.IsValid) { await LoadLists(model.RoomId, siteId); return View(model); }

        await ApplyRelations(model);
        _db.Employees.Add(model); await _db.SaveChangesAsync();
        var vehicleWarning = await _transportAssignmentService.ReserveSeatForHousingAsync(model);
        await RefreshRoomStatus(model.RoomId);
        await transaction.CommitAsync();
        if (vehicleWarning != null) TempData["VehicleAssignmentWarning"] = vehicleWarning;
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.Employees
            .Include(e => e.Room)
            .ThenInclude(r => r!.Apartment)
            .ThenInclude(a => a!.Building)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (item == null) return NotFound();
        await LoadLists(item.RoomId, item.Room?.Apartment?.Building?.SiteId);
        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Employee model, int? siteId)
    {
        if (id != model.Id) return NotFound();
        var old = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id); if (old == null) return NotFound();
        if (string.IsNullOrWhiteSpace(model.EmployeeCode)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف مطلوب");
        else if (await _db.Employees.AnyAsync(e => e.EmployeeCode == model.EmployeeCode && e.Id != id)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف مستخدم لموظف آخر");

        await ValidateRoomSelection(model.RoomId, siteId, id, old.RoomId);
        if (!ModelState.IsValid) { await LoadLists(model.RoomId ?? old.RoomId, siteId); return View(model); }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        await ValidateRoomSelection(model.RoomId, siteId, id, old.RoomId);
        if (!ModelState.IsValid) { await LoadLists(model.RoomId ?? old.RoomId, siteId); return View(model); }

        await ApplyRelations(model);
        _db.Update(model); await _db.SaveChangesAsync();
        var vehicleWarning = await _transportAssignmentService.ReserveSeatForHousingAsync(model);
        await RefreshRoomStatus(old.RoomId); await RefreshRoomStatus(model.RoomId);
        await transaction.CommitAsync();
        if (vehicleWarning != null) TempData["VehicleAssignmentWarning"] = vehicleWarning;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.Employees.FindAsync(id); if (item == null) return NotFound();
        var roomId = item.RoomId; _db.Employees.Remove(item); await _db.SaveChangesAsync(); await RefreshRoomStatus(roomId);
        return RedirectToAction(nameof(Index));
    }

    private async Task ApplyRelations(Employee model)
    {
        if (model.ProjectId.HasValue)
        {
            var project = await _db.Projects.FindAsync(model.ProjectId.Value);
            model.ProjectName = project?.Name ?? "";
        }
        else model.ProjectName = "";

        if (model.RoomId.HasValue)
        {
            var room = await _db.Rooms.Include(r => r.Apartment).ThenInclude(a => a!.Building).FirstOrDefaultAsync(r => r.Id == model.RoomId.Value);
            if (room != null)
            {
                model.RoomNo = room.Number;
                model.ApartmentNo = room.Apartment?.Number;
                model.BuildingNo = room.Apartment?.Building?.Number;
            }
        }
        else { model.RoomNo = null; model.ApartmentNo = null; model.BuildingNo = null; }
    }

    private async Task ValidateRoomSelection(
        int? roomId,
        int? siteId,
        int? employeeId,
        int? currentRoomId)
    {
        if (!roomId.HasValue) return;

        var room = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .FirstOrDefaultAsync(r => r.Id == roomId.Value);
        if (room?.Apartment?.Building == null)
        {
            ModelState.AddModelError(nameof(Employee.RoomId), "الغرفة المحددة غير موجودة");
            return;
        }

        if (room.Apartment.Building.SiteId != siteId)
        {
            ModelState.AddModelError(nameof(Employee.RoomId), "اختر غرفة تابعة للمنطقة المحددة");
            return;
        }

        if (roomId == currentRoomId) return;

        var occupiedByEmployee = await _db.Employees.AnyAsync(e =>
            e.RoomId == roomId
            && e.IsActive
            && e.ResignationDate == null
            && (!employeeId.HasValue || e.Id != employeeId.Value));
        var reservedByPendingAssignment = await _db.PendingHousingAssignments.AnyAsync(a =>
            a.RoomId == roomId && !a.IsApplied);
        if (room.IsOccupied || occupiedByEmployee || reservedByPendingAssignment)
        {
            ModelState.AddModelError(nameof(Employee.RoomId), "الغرفة مشغولة أو محجوزة بتعيين منتظر");
        }
    }

    private async Task RefreshRoomStatus(int? roomId)
    {
        if (!roomId.HasValue) return;
        var occupied = await _db.Employees.AnyAsync(e => e.RoomId == roomId && e.IsActive && e.ResignationDate == null);
        var room = await _db.Rooms.FindAsync(roomId.Value);
        if (room != null) { room.IsOccupied = occupied; await _db.SaveChangesAsync(); }
    }
}
