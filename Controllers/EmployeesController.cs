using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize]
public class EmployeesController : Controller
{
    private readonly AppDbContext _db;
    public EmployeesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.Employees.Include(e => e.Project).Include(e => e.Room).ThenInclude(r => r!.Apartment).ThenInclude(a => a!.Building).ThenInclude(b => b!.Site).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(e => e.FullName.Contains(q) || e.NationalId.Contains(q) || e.Phone.Contains(q) || e.ProjectName.Contains(q));
        ViewBag.Q = q;
        return View(await query.OrderByDescending(e => e.Id).ToListAsync());
    }

    private async Task LoadLists()
    {
        ViewBag.Projects = await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Rooms = await _db.Rooms.Include(r => r.Apartment).ThenInclude(a => a!.Building).ThenInclude(b => b!.Site).OrderBy(r => r.Apartment!.Building!.Site!.Name).ThenBy(r => r.Apartment!.Building!.Number).ThenBy(r => r.Apartment!.Number).ThenBy(r => r.Number).ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        await LoadLists();
        return View(new Employee { HousingDate = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee model)
    {
        if (string.IsNullOrWhiteSpace(model.EmployeeCode)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف مطلوب");
        else if (await _db.Employees.AnyAsync(e => e.EmployeeCode == model.EmployeeCode)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف موجود بالفعل");
        if (!ModelState.IsValid) { await LoadLists(); return View(model); }
        await ApplyRelations(model);
        _db.Employees.Add(model); await _db.SaveChangesAsync();
        await RefreshRoomStatus(model.RoomId);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.Employees.FindAsync(id); if (item == null) return NotFound();
        await LoadLists(); return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Employee model)
    {
        if (id != model.Id) return NotFound();
        var old = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id); if (old == null) return NotFound();
        if (string.IsNullOrWhiteSpace(model.EmployeeCode)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف مطلوب");
        else if (await _db.Employees.AnyAsync(e => e.EmployeeCode == model.EmployeeCode && e.Id != id)) ModelState.AddModelError(nameof(model.EmployeeCode), "كود الموظف مستخدم لموظف آخر");
        if (!ModelState.IsValid) { await LoadLists(); return View(model); }
        await ApplyRelations(model);
        _db.Update(model); await _db.SaveChangesAsync();
        await RefreshRoomStatus(old.RoomId); await RefreshRoomStatus(model.RoomId);
        return RedirectToAction(nameof(Index));
    }

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

    private async Task RefreshRoomStatus(int? roomId)
    {
        if (!roomId.HasValue) return;
        var occupied = await _db.Employees.AnyAsync(e => e.RoomId == roomId && e.IsActive && e.ResignationDate == null);
        var room = await _db.Rooms.FindAsync(roomId.Value);
        if (room != null) { room.IsOccupied = occupied; await _db.SaveChangesAsync(); }
    }
}
