using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using HousingManagementWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace HousingManagementWeb.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _db.AppUsers
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return View(users);
    }

    public async Task<IActionResult> Create()
    {
        await LoadRoles();
        return View(new AppUser());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AppUser model, int[] selectedRoles)
    {
        model.UserName = model.UserName?.Trim() ?? "";
        model.Email = model.Email?.Trim() ?? "";
        model.FullName = model.FullName?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(model.UserName)) ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم مطلوب");
        if (string.IsNullOrWhiteSpace(model.Email)) ModelState.AddModelError(nameof(model.Email), "البريد الإلكتروني مطلوب");
        if (string.IsNullOrWhiteSpace(model.FullName)) ModelState.AddModelError(nameof(model.FullName), "الاسم الكامل مطلوب");
        if (string.IsNullOrWhiteSpace(model.PasswordHash)) ModelState.AddModelError(nameof(model.PasswordHash), "كلمة المرور مطلوبة");

        if (await _db.AppUsers.AnyAsync(u => u.UserName == model.UserName)) ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم موجود بالفعل");
        if (await _db.AppUsers.AnyAsync(u => u.Email == model.Email)) ModelState.AddModelError(nameof(model.Email), "البريد الإلكتروني موجود بالفعل");
        await ValidateSelectedRoles(selectedRoles);

        if (!ModelState.IsValid)
        {
            await LoadRoles();
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable);
        model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.PasswordHash);
        model.CreatedAt = DateTime.Now;
        _db.AppUsers.Add(model);
        await _db.SaveChangesAsync();

        foreach (var roleId in selectedRoles)
        {
            _db.UserRoles.Add(new UserRole { UserId = model.Id, RoleId = roleId });
        }
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.AppUsers
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();
        await LoadRoles();
        ViewBag.SelectedRoles = user.UserRoles.Select(ur => ur.RoleId).ToList();
        return View(user);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AppUser model, int[] selectedRoles)
    {
        var user = await _db.AppUsers
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        model.UserName = model.UserName?.Trim() ?? "";
        model.Email = model.Email?.Trim() ?? "";
        model.FullName = model.FullName?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(model.PasswordHash))
        {
            ModelState.Remove(nameof(model.PasswordHash));
        }
        if (string.IsNullOrWhiteSpace(model.UserName)) ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم مطلوب");
        if (string.IsNullOrWhiteSpace(model.FullName)) ModelState.AddModelError(nameof(model.FullName), "الاسم الكامل مطلوب");
        if (string.IsNullOrWhiteSpace(model.Email)
            || !new EmailAddressAttribute().IsValid(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "أدخل بريدًا إلكترونيًا صحيحًا");
        }
        if (await _db.AppUsers.AnyAsync(existing =>
                existing.Id != id && existing.UserName == model.UserName))
        {
            ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم موجود بالفعل");
        }
        if (await _db.AppUsers.AnyAsync(existing =>
                existing.Id != id && existing.Email == model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "البريد الإلكتروني موجود بالفعل");
        }
        await ValidateSelectedRoles(selectedRoles);
        await ValidateLastActiveAdminChange(id, user.IsActive, model.IsActive, selectedRoles);

        if (!ModelState.IsValid)
        {
            await LoadRoles();
            ViewBag.SelectedRoles = selectedRoles;
            return View(model);
        }

        user.UserName = model.UserName.Trim();
        user.FullName = model.FullName.Trim();
        user.Email = model.Email.Trim();
        user.IsActive = model.IsActive;

        if (!string.IsNullOrWhiteSpace(model.PasswordHash))
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.PasswordHash);
        }

        _db.UserRoles.RemoveRange(user.UserRoles);

        foreach (var roleId in selectedRoles)
        {
            _db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.AppUsers.FindAsync(id);
        if (user != null)
        {
            await ValidateLastActiveAdminChange(id, user.IsActive, false, []);
            if (!ModelState.IsValid)
            {
                TempData["UserError"] = "لا يمكن حذف آخر مسؤول نشط.";
                return RedirectToAction(nameof(Index));
            }

            _db.AppUsers.Remove(user);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadRoles()
    {
        ViewBag.Roles = await _db.AppRoles.OrderBy(r => r.Name).ToListAsync();
    }

    private async Task ValidateSelectedRoles(int[] selectedRoles)
    {
        var distinctRoleIds = selectedRoles.Distinct().ToArray();
        if (distinctRoleIds.Length != selectedRoles.Length
            || await _db.AppRoles.CountAsync(role => distinctRoleIds.Contains(role.Id)) != distinctRoleIds.Length)
        {
            ModelState.AddModelError(string.Empty, "اختيار الأدوار غير صالح");
        }
    }

    private async Task ValidateLastActiveAdminChange(
        int userId,
        bool wasActive,
        bool willBeActive,
        int[] selectedRoles)
    {
        if (!wasActive
            || !await _db.UserRoles.AnyAsync(userRole =>
                userRole.UserId == userId && userRole.Role.Name == "Admin"))
        {
            return;
        }

        var adminRoleId = await _db.AppRoles
            .Where(role => role.Name == "Admin")
            .Select(role => (int?)role.Id)
            .FirstOrDefaultAsync();
        var willRemainAdmin = willBeActive
            && adminRoleId.HasValue
            && selectedRoles.Contains(adminRoleId.Value);
        if (willRemainAdmin) return;

        var anotherActiveAdminExists = await _db.AppUsers.AnyAsync(user =>
            user.Id != userId
            && user.IsActive
            && user.UserRoles.Any(userRole => userRole.Role.Name == "Admin"));
        if (!anotherActiveAdminExists)
        {
            ModelState.AddModelError(string.Empty, "لا يمكن تعطيل أو إزالة دور آخر مسؤول نشط.");
        }
    }
}
