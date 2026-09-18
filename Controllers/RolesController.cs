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
        if (!ModelState.IsValid)
        {
            await LoadPermissions();
            return View(model);
        }

        _db.AppRoles.Add(model);
        await _db.SaveChangesAsync();

        foreach (var permissionId in selectedPermissions)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = model.Id, PermissionId = permissionId });
        }

        await _db.SaveChangesAsync();
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

        role.Name = model.Name;
        role.Description = model.Description;

        _db.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var permissionId in selectedPermissions)
        {
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var role = await _db.AppRoles.FindAsync(id);
        if (role != null)
        {
            _db.AppRoles.Remove(role);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadPermissions()
    {
        ViewBag.Permissions = await _db.Permissions.OrderBy(p => p.Name).ToListAsync();
    }
}
