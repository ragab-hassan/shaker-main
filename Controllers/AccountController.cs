using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using HousingManagementWeb.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _db;

    public AccountController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string userName, string password, string? returnUrl = null)
    {
        var user = await _db.AppUsers
            .FirstOrDefaultAsync(u => u.UserName == userName && u.IsActive);

        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة");
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        await AuthHelper.SignInAsync(HttpContext, user, _db);

        if (!string.IsNullOrWhiteSpace(returnUrl))
            return LocalRedirect(returnUrl);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
