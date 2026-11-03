using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using HousingManagementWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize(Policy = AppPermissions.ManageHousingAssignments)]
public class PendingHousingAssignmentsController : Controller
{
    private readonly AppDbContext _db;
    private readonly EmployeeTransportAssignmentService _transportAssignmentService;

    public PendingHousingAssignmentsController(
        AppDbContext db,
        EmployeeTransportAssignmentService transportAssignmentService)
    {
        _db = db;
        _transportAssignmentService = transportAssignmentService;
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
        var employee = await ValidateEmployee(model);
        var room = await ValidateRoom(model.RoomId, model.SiteId);
        await ValidateProject(model.ProjectId);

        if (room != null && await HasRoomConflict(room.Id, null, model.EmployeeId))
        {
            ModelState.AddModelError(nameof(model.RoomId), "الغرفة مشغولة أو مرتبطة بتعيين آخر غير مطبق");
        }

        if (!ModelState.IsValid)
        {
            await LoadLists(model.RoomId);
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        if (await HasRoomConflict(room!.Id, null, model.EmployeeId))
        {
            ModelState.AddModelError(nameof(model.RoomId), "الغرفة مشغولة أو مرتبطة بتعيين آخر غير مطبق");
            await LoadLists(model.RoomId);
            return View(model);
        }

        model.EmployeeName = employee?.FullName ?? model.EmployeeName.Trim();
        model.IsApplied = false;
        _db.PendingHousingAssignments.Add(model);
        await _db.SaveChangesAsync();

        if (model.HousingDate.Date <= DateTime.Today)
        {
            var error = await ApplyPendingAssignment(model);
            if (error != null) TempData["AssignmentWarning"] = error;
        }

        await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _db.PendingHousingAssignments
            .Include(assignment => assignment.Employee)
            .Include(assignment => assignment.Room)
            .ThenInclude(room => room!.Apartment)
            .ThenInclude(apartment => apartment!.Building)
            .ThenInclude(building => building!.Site)
            .FirstOrDefaultAsync(assignment => assignment.Id == id);
        if (item == null) return NotFound();
        if (!item.IsApplied && item.EmployeeId.HasValue) item.EmployeeName = "";
        await LoadLists(item.RoomId);
        return View(item);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PendingHousingAssignment model)
    {
        var item = await _db.PendingHousingAssignments.FirstOrDefaultAsync(x => x.Id == id);
        if (item == null) return NotFound();
        if (id != model.Id) return NotFound();

        if (item.IsApplied && (model.EmployeeId != item.EmployeeId || model.RoomId != item.RoomId))
        {
            ModelState.AddModelError(string.Empty, "لا يمكن تغيير الموظف أو الغرفة بعد تطبيق التعيين.");
        }

        if (item.IsApplied)
        {
            model.EmployeeId = item.EmployeeId;
            model.RoomId = item.RoomId;
        }

        var employeeId = model.EmployeeId;
        var employee = await ValidateEmployee(model);
        var roomId = model.RoomId;
        var siteId = item.IsApplied ? item.SiteId : model.SiteId;
        var room = await ValidateRoom(roomId, siteId);
        await ValidateProject(model.ProjectId);

        if (room != null && await HasRoomConflict(room.Id, item.Id, employeeId, item.IsApplied))
        {
            ModelState.AddModelError(nameof(model.RoomId), "الغرفة مشغولة أو مرتبطة بتعيين آخر غير مطبق");
        }

        if (!ModelState.IsValid)
        {
            await LoadLists(item.RoomId);
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        if (room != null && await HasRoomConflict(room.Id, item.Id, employeeId, item.IsApplied))
        {
            ModelState.AddModelError(nameof(model.RoomId), "الغرفة مشغولة أو مرتبطة بتعيين آخر غير مطبق");
            await LoadLists(item.RoomId);
            return View(model);
        }

        item.EmployeeId = employeeId;
        item.EmployeeName = employee?.FullName ?? model.EmployeeName.Trim();
        item.JobTitle = model.JobTitle;
        item.ProjectId = model.ProjectId;
        item.SiteId = siteId;
        item.RoomId = roomId;
        item.HousingDate = model.HousingDate;

        await _db.SaveChangesAsync();

        if (!item.IsApplied && item.HousingDate.Date <= DateTime.Today)
        {
            var error = await ApplyPendingAssignment(item);
            if (error != null) TempData["AssignmentWarning"] = error;
        }
        else if (item.IsApplied && item.EmployeeId.HasValue)
        {
            var employeeRecord = await _db.Employees.FindAsync(item.EmployeeId.Value);
            if (employeeRecord != null)
            {
                employeeRecord.JobTitle = item.JobTitle;
                employeeRecord.ProjectId = item.ProjectId;
                employeeRecord.ProjectName = (await _db.Projects.FindAsync(item.ProjectId))?.Name ?? "";
                employeeRecord.HousingDate = item.HousingDate;
                await _db.SaveChangesAsync();
                var vehicleWarning = await _transportAssignmentService.ReserveSeatForHousingAsync(employeeRecord);
                if (vehicleWarning != null) AddVehicleAssignmentWarning(vehicleWarning);
            }
        }

        await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
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

    private async Task<Employee?> ValidateEmployee(PendingHousingAssignment model)
    {
        if (model.EmployeeId.HasValue)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e =>
                e.Id == model.EmployeeId.Value && e.IsActive && e.ResignationDate == null);
            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "الموظف المحدد غير موجود أو غير نشط");
            }
            return employee;
        }

        if (string.IsNullOrWhiteSpace(model.EmployeeName))
        {
            ModelState.AddModelError(nameof(model.EmployeeName), "اختر موظفًا قائمًا أو أدخل اسم الموظف الجديد");
        }
        return null;
    }

    private async Task<Room?> ValidateRoom(int roomId, int siteId)
    {
        var room = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .FirstOrDefaultAsync(r => r.Id == roomId);

        if (room == null)
        {
            ModelState.AddModelError(nameof(PendingHousingAssignment.RoomId), "الغرفة المحددة غير موجودة");
            return null;
        }

        if (room.Apartment?.Building?.SiteId != siteId)
        {
            ModelState.AddModelError(nameof(PendingHousingAssignment.RoomId), "الغرفة لا تتبع المنطقة المحددة");
        }
        return room;
    }

    private async Task ValidateProject(int projectId)
    {
        if (!await _db.Projects.AnyAsync(project => project.Id == projectId && project.IsActive))
        {
            ModelState.AddModelError(nameof(PendingHousingAssignment.ProjectId), "المشروع المحدد غير موجود أو غير نشط");
        }
    }

    private async Task<bool> HasRoomConflict(
        int roomId,
        int? excludedAssignmentId,
        int? employeeId,
        bool allowCurrentOccupant = false)
    {
        var roomStatus = await _db.Rooms
            .AsNoTracking()
            .Where(room => room.Id == roomId)
            .Select(room => new { room.IsOccupied })
            .FirstOrDefaultAsync();
        if (roomStatus == null) return true;

        if (!allowCurrentOccupant)
        {
            var employeeOccupiesRoom = employeeId.HasValue && await _db.Employees.AnyAsync(employee =>
                employee.Id == employeeId.Value
                && employee.RoomId == roomId
                && employee.IsActive
                && employee.ResignationDate == null);
            var hasOtherOccupant = await _db.Employees.AnyAsync(employee =>
                employee.RoomId == roomId
                && employee.IsActive
                && employee.ResignationDate == null
                && (!employeeId.HasValue || employee.Id != employeeId.Value));
            if ((roomStatus.IsOccupied && !employeeOccupiesRoom) || hasOtherOccupant) return true;
        }

        return await _db.PendingHousingAssignments.AnyAsync(assignment =>
            assignment.RoomId == roomId
            && !assignment.IsApplied
            && (!excludedAssignmentId.HasValue || assignment.Id != excludedAssignmentId.Value));
    }

    private async Task LoadLists(int? selectedRoomId = null)
    {
        ViewBag.Employees = await _db.Employees
            .Where(e => e.IsActive && e.ResignationDate == null)
            .OrderBy(e => e.FullName)
            .ToListAsync();
        ViewBag.Projects = await _db.Projects.Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();
        ViewBag.Sites = await _db.Sites.OrderBy(s => s.Name).ToListAsync();
        ViewBag.Rooms = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .ThenInclude(b => b!.Site)
            .Where(r => (!r.IsOccupied
                    && !_db.Employees.Any(employee =>
                        employee.RoomId == r.Id && employee.IsActive && employee.ResignationDate == null)
                    && !_db.PendingHousingAssignments.Any(assignment => assignment.RoomId == r.Id && !assignment.IsApplied))
                || r.Id == selectedRoomId)
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
        var conflicts = new List<int>();

        foreach (var item in dueItems)
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable);
            if (await ApplyPendingAssignment(item) is not null)
            {
                conflicts.Add(item.Id);
            }
            await transaction.CommitAsync();
        }

        if (conflicts.Count > 0)
        {
            TempData["AssignmentWarning"] =
                $"تعذر تطبيق التعيينات ({string.Join(", ", conflicts)}) بسبب إشغال الغرفة أو وجود تعارض؛ راجعها قبل إعادة المحاولة.";
        }
    }

    private async Task<string?> ApplyPendingAssignment(PendingHousingAssignment item)
    {
        if (item.IsApplied) return null;

        var employee = item.EmployeeId.HasValue
            ? await _db.Employees.FirstOrDefaultAsync(e => e.Id == item.EmployeeId.Value)
            : null;
        if (item.EmployeeId.HasValue && employee == null)
        {
            return $"تعذر تطبيق التعيين {item.Id}: الموظف المحدد لم يعد موجودًا.";
        }
        if (employee != null && (!employee.IsActive || employee.ResignationDate != null))
        {
            return $"تعذر تطبيق التعيين {item.Id}: الموظف المحدد غير نشط أو مستقيل.";
        }

        if (await HasRoomConflict(item.RoomId, item.Id, item.EmployeeId))
        {
            return $"تعذر تطبيق التعيين {item.Id}: الغرفة مشغولة أو يوجد تعيين آخر متعارض.";
        }

        var room = await _db.Rooms
            .Include(r => r.Apartment)
            .ThenInclude(a => a!.Building)
            .FirstOrDefaultAsync(r => r.Id == item.RoomId);
        if (room == null)
        {
            return $"تعذر تطبيق التعيين {item.Id}: الغرفة لم تعد موجودة.";
        }

        if (employee == null)
        {
            var project = await _db.Projects.FindAsync(item.ProjectId);
            employee = new Employee
            {
                FullName = item.EmployeeName.Trim(),
                EmployeeCode = $"TEMP-{Guid.NewGuid():N}",
                JobTitle = item.JobTitle,
                ProjectId = item.ProjectId,
                ProjectName = project?.Name ?? "",
                IsActive = true
            };
            _db.Employees.Add(employee);
        }

        var previousRoomId = employee.RoomId;
        var projectForEmployee = await _db.Projects.FindAsync(item.ProjectId);
        employee.ProjectId = item.ProjectId;
        employee.ProjectName = projectForEmployee?.Name ?? "";
        employee.RoomId = item.RoomId;
        employee.RoomNo = room.Number;
        employee.ApartmentNo = room.Apartment?.Number;
        employee.BuildingNo = room.Apartment?.Building?.Number;
        employee.HousingDate = item.HousingDate;
        employee.JobTitle = item.JobTitle;
        employee.IsActive = true;
        item.Employee = employee;
        room.IsOccupied = true;
        item.IsApplied = true;
        await _db.SaveChangesAsync();

        var vehicleWarning = await _transportAssignmentService.ReserveSeatForHousingAsync(employee);
        if (vehicleWarning != null) AddVehicleAssignmentWarning(vehicleWarning);

        if (previousRoomId.HasValue && previousRoomId != room.Id)
        {
            var oldRoom = await _db.Rooms.FindAsync(previousRoomId.Value);
            if (oldRoom != null)
            {
                oldRoom.IsOccupied = await _db.Employees.AnyAsync(e =>
                    e.RoomId == oldRoom.Id && e.IsActive && e.ResignationDate == null);
                await _db.SaveChangesAsync();
            }
        }
        return null;
    }

    private void AddVehicleAssignmentWarning(string warning)
    {
        var previousWarning = TempData.Peek("VehicleAssignmentWarning") as string;
        TempData["VehicleAssignmentWarning"] = string.IsNullOrEmpty(previousWarning)
            ? warning
            : $"{previousWarning} {warning}";
    }
}
