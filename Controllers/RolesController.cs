using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize(Roles = "Admin")]
public class RolesController : Controller
{
    private readonly AppDbContext _db;

    public RolesController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var roles = await _db.AppRoles
            .Include(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .OrderBy(r => r.Name)
            .ToListAsync();

        return View(roles);
    }

    public async Task<IActionResult> Create()
    {
        await LoadPermissions();
        return View(new AppRole());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppRole model, int[] selectedPermissions)
    {
        if (string.IsNullOrWhiteSpace(model.Name)) ModelState.AddModelError(nameof(model.Name), "اسم الدور مطلوب");
        model.Name = model.Name?.Trim() ?? "";
        if (await _db.AppRoles.AnyAsync(role => role.Name == model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم الدور موجود بالفعل");
        }
        await ValidateSelectedPermissions(selectedPermissions);
        if (!ModelState.IsValid)
        {
            await LoadPermissions();
            return View(model);
        }

        model.IsSystemRole = false;
        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        _db.AppRoles.Add(model);
        await _db.SaveChangesAsync();

        foreach (var permissionId in selectedPermissions.Distinct())
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = model.Id, PermissionId = permissionId });
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var role = await _db.AppRoles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound();

        await LoadPermissions();
        ViewBag.SelectedPermissions = role.RolePermissions.Select(rp => rp.PermissionId).ToList();
        return View(role);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppRole model, int[] selectedPermissions)
    {
        var role = await _db.AppRoles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (role == null) return NotFound();

        model.Name = model.Name?.Trim() ?? "";
        if (role.IsSystemRole && model.Name != role.Name)
        {
            ModelState.AddModelError(nameof(model.Name), "لا يمكن تغيير اسم دور النظام");
        }
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم الدور مطلوب");
        }
        if (await _db.AppRoles.AnyAsync(existing =>
                existing.Id != id && existing.Name == model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم الدور موجود بالفعل");
        }
        await ValidateSelectedPermissions(selectedPermissions);

        if (!ModelState.IsValid)
        {
            await LoadPermissions();
            ViewBag.SelectedPermissions = selectedPermissions;
            return View(model);
        }

        role.Name = model.Name;
        role.Description = model.Description;

        _db.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var permissionId in selectedPermissions.Distinct())
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.AppRoles.FindAsync(id);
        if (role != null)
        {
            if (role.IsSystemRole)
            {
                TempData["RoleError"] = "لا يمكن حذف دور أساسي يستخدمه النظام.";
                return RedirectToAction(nameof(Index));
            }

            _db.AppRoles.Remove(role);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadPermissions()
    {
        ViewBag.Permissions = await _db.Permissions.OrderBy(p => p.Name).ToListAsync();
    }

    private async Task ValidateSelectedPermissions(int[] selectedPermissions)
    {
        var distinctPermissionIds = selectedPermissions.Distinct().ToArray();
        if (distinctPermissionIds.Length != selectedPermissions.Length
            || await _db.Permissions.CountAsync(permission =>
                distinctPermissionIds.Contains(permission.Id)) != distinctPermissionIds.Length)
        {
            ModelState.AddModelError(string.Empty, "اختيار الصلاحيات غير صالح");
        }
    }
}
