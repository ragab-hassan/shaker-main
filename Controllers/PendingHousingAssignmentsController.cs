using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize]
public class PendingHousingAssignmentsController : Controller
{
    private readonly AppDbContext _db;

    public PendingHousingAssignmentsController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        await ApplyDueAssignments();

        var items = await _db.PendingHousingAssignments
            .Include(x => x.Employee)
            .Include(x => x.Project)
            .Include(x => x.Site)
            .Include(x => x.Room)
            .ThenInclude(r => r!.Apartment)
            .ThenInclude(a => a!.Building)
            .OrderBy(x => x.HousingDate)
            .ToListAsync();

        return View(items);
    }

    public async Task<IActionResult> Create()
    {
        await LoadLists();
        return View(new PendingHousingAssignment { HousingDate = DateTime.Today });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PendingHousingAssignment model)
    {
        if (string.IsNullOrWhiteSpace(model.EmployeeName))
            ModelState.AddModelError(nameof(model.EmployeeName), "اسم الموظف مطلوب");

        if (!ModelState.IsValid)
        {
            await LoadLists();
            return View(model);
        }

        if (model.SiteId == 0 || model.ProjectId == 0 || model.RoomId == 0)
        {
            ModelState.AddModelError(string.Empty, "جميع الحقول مطلوبة");
            await LoadLists();
            return View(model);
        }

        var room = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .ThenInclude(b => b!.Site)
            .FirstOrDefaultAsync(r => r.Id == model.RoomId);

        if (room == null || room.IsOccupied)
        {
            ModelState.AddModelError(nameof(model.RoomId), "الغرفة مختارة غير متاحة أو مشغولة");
            await LoadLists();
            return View(model);
        }

        model.IsApplied = false;
        _db.PendingHousingAssignments.Add(model);
        await _db.SaveChangesAsync();

        if (model.HousingDate <= DateTime.Today)
        {
            await ApplyPendingAssignment(model);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.PendingHousingAssignments.FindAsync(id);
        if (item == null) return NotFound();
        await LoadLists();
        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PendingHousingAssignment model)
    {
        if (id != model.Id) return NotFound();

        if (!ModelState.IsValid)
        {
            await LoadLists();
            return View(model);
        }

        var room = await _db.Rooms.FindAsync(model.RoomId);
        if (room == null || room.IsOccupied)
        {
            ModelState.AddModelError(nameof(model.RoomId), "الغرفة مختارة غير متاحة أو مشغولة");
            await LoadLists();
            return View(model);
        }

        _db.Update(model);
        await _db.SaveChangesAsync();

        if (model.HousingDate <= DateTime.Today)
        {
            await ApplyPendingAssignment(model);
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var item = await _db.PendingHousingAssignments.FindAsync(id);
        if (item != null)
        {
            _db.PendingHousingAssignments.Remove(item);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task LoadLists()
    {
        ViewBag.Employees = await _db.Employees.Where(e => e.IsActive && e.ResignationDate == null).OrderBy(e => e.FullName).ToListAsync();
        ViewBag.Projects = await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Sites = await _db.Sites.OrderBy(s => s.Name).ToListAsync();
        ViewBag.Rooms = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .ThenInclude(b => b!.Site)
            .Where(r => !r.IsOccupied)
            .OrderBy(r => r.Apartment!.Building!.Site!.Name)
            .ThenBy(r => r.Apartment!.Building!.Number)
            .ThenBy(r => r.Apartment!.Number)
            .ThenBy(r => r.Number)
            .ToListAsync();
    }

    private async Task ApplyDueAssignments()
    {
        var dueItems = await _db.PendingHousingAssignments
            .Where(x => !x.IsApplied && x.HousingDate <= DateTime.Today)
            .ToListAsync();

        foreach (var item in dueItems)
        {
            await ApplyPendingAssignment(item);
        }
    }

    private async Task ApplyPendingAssignment(PendingHousingAssignment item)
    {
        var employee = await _db.Employees.FirstOrDefaultAsync(e => e.FullName.Trim() == item.EmployeeName.Trim());
        var room = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .FirstOrDefaultAsync(r => r.Id == item.RoomId);

        if (room == null)
        {
            return;
        }

        if (employee == null)
        {
            employee = new Employee
            {
                FullName = item.EmployeeName.Trim(),
                EmployeeCode = $"TEMP-{DateTime.Now:yyyyMMddHHmmss}",
                JobTitle = item.JobTitle,
                ProjectId = item.ProjectId,
                ProjectName = (await _db.Projects.FindAsync(item.ProjectId))?.Name ?? "",
                IsActive = true,
                HousingDate = item.HousingDate,
                RoomId = item.RoomId,
                RoomNo = room.Number,
                ApartmentNo = room.Apartment?.Number,
                BuildingNo = room.Apartment?.Building?.Number
            };
            _db.Employees.Add(employee);
        }
        else
        {
            var project = await _db.Projects.FindAsync(item.ProjectId);
            employee.ProjectId = item.ProjectId;
            employee.ProjectName = project?.Name ?? "";
            employee.RoomId = item.RoomId;
            employee.RoomNo = room.Number;
            employee.ApartmentNo = room.Apartment?.Number;
            employee.BuildingNo = room.Apartment?.Building?.Number;
            employee.HousingDate = item.HousingDate;
            employee.JobTitle = item.JobTitle;
            employee.IsActive = true;
        }

        room.IsOccupied = true;
        item.IsApplied = true;
        await _db.SaveChangesAsync();
    }
}
