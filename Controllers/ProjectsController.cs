using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace HousingManagementWeb.Controllers;

[Authorize]
public class ProjectsController : Controller
{
    private readonly AppDbContext _db;
    public ProjectsController(AppDbContext db) => _db = db;
    public async Task<IActionResult> Index() => View(await _db.Projects.OrderBy(p=>p.Name).ToListAsync());
    public IActionResult Create() => View(new Project());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Project model){ if(!ModelState.IsValid)return View(model);_db.Projects.Add(model);await _db.SaveChangesAsync();return RedirectToAction(nameof(Index));}
    public async Task<IActionResult> Edit(int id){var x=await _db.Projects.FindAsync(id);return x==null?NotFound():View(x);}
    [HttpPost,ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Project model){if(!ModelState.IsValid)return View(model);_db.Update(model);await _db.SaveChangesAsync();return RedirectToAction(nameof(Index));}
    public async Task<IActionResult> Delete(int id){var x=await _db.Projects.FindAsync(id);if(x!=null){_db.Remove(x);await _db.SaveChangesAsync();}return RedirectToAction(nameof(Index));}
}
