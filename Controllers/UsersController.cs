using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using HousingManagementWeb.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
        if (string.IsNullOrWhiteSpace(model.UserName)) ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم مطلوب");
        if (string.IsNullOrWhiteSpace(model.Email)) ModelState.AddModelError(nameof(model.Email), "البريد الإلكتروني مطلوب");
        if (string.IsNullOrWhiteSpace(model.FullName)) ModelState.AddModelError(nameof(model.FullName), "الاسم الكامل مطلوب");
        if (string.IsNullOrWhiteSpace(model.PasswordHash)) ModelState.AddModelError(nameof(model.PasswordHash), "كلمة المرور مطلوبة");

        if (await _db.AppUsers.AnyAsync(u => u.UserName == model.UserName)) ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم موجود بالفعل");
        if (await _db.AppUsers.AnyAsync(u => u.Email == model.Email)) ModelState.AddModelError(nameof(model.Email), "البريد الإلكتروني موجود بالفعل");

        if (!ModelState.IsValid)
        {
            await LoadRoles();
            return View(model);
        }

        model.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.PasswordHash);
        model.CreatedAt = DateTime.Now;
        _db.AppUsers.Add(model);
        await _db.SaveChangesAsync();

        foreach (var roleId in selectedRoles)
        {
            _db.UserRoles.Add(new UserRole { UserId = model.Id, RoleId = roleId });
        }
        await _db.SaveChangesAsync();

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
    public async Task<IActionResult> Edit(int id, AppUser model, int[] selectedRoles, bool changePassword)
    {
        var user = await _db.AppUsers
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        user.UserName = model.UserName;
        user.FullName = model.FullName;
        user.Email = model.Email;
        user.IsActive = model.IsActive;

        if (changePassword && !string.IsNullOrWhiteSpace(model.PasswordHash))
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

    public async Task<IActionResult> Delete(int id)
    {
        var user = await _db.AppUsers.FindAsync(id);
        if (user != null)
        {
            _db.AppUsers.Remove(user);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadRoles()
    {
        ViewBag.Roles = await _db.AppRoles.OrderBy(r => r.Name).ToListAsync();
    }
}
