using HousingManagementWeb.Data;
using HousingManagementWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HousingManagementWeb.Controllers;

[Authorize]
public class LocationsController : Controller
{
    private readonly AppDbContext _db;
    public LocationsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index() => View(await _db.Sites
        .Include(s => s.Buildings).ThenInclude(b => b.Apartments).ThenInclude(a => a.Rooms)
        .OrderBy(s => s.Name).ToListAsync());

    public IActionResult CreateSite() => View(new Site());
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSite(Site x)
    { if (!ModelState.IsValid) return View(x); _db.Sites.Add(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }

    public async Task<IActionResult> EditSite(int id)
    { var x = await _db.Sites.FindAsync(id); return x == null ? NotFound() : View(x); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSite(int id, Site x)
    { if (id != x.Id) return NotFound(); if (!ModelState.IsValid) return View(x); _db.Update(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> DeleteSite(int id)
    { var x = await _db.Sites.FindAsync(id); if (x != null) { _db.Sites.Remove(x); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }

    public async Task<IActionResult> CreateBuilding(int siteId) { await LoadSites(); return View(new Building { SiteId = siteId }); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateBuilding(Building x)
    { if (!ModelState.IsValid) { await LoadSites(); return View(x); } _db.Buildings.Add(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> EditBuilding(int id) { var x = await _db.Buildings.FindAsync(id); if (x == null) return NotFound(); await LoadSites(); return View(x); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBuilding(int id, Building x)
    { if (id != x.Id) return NotFound(); if (!ModelState.IsValid) { await LoadSites(); return View(x); } _db.Update(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> DeleteBuilding(int id)
    { var x = await _db.Buildings.FindAsync(id); if (x != null) { _db.Buildings.Remove(x); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }

    public async Task<IActionResult> CreateApartment(int buildingId) { await LoadBuildings(); return View(new Apartment { BuildingId = buildingId }); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApartment(Apartment x)
    { if (!ModelState.IsValid) { await LoadBuildings(); return View(x); } _db.Apartments.Add(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> EditApartment(int id) { var x = await _db.Apartments.FindAsync(id); if (x == null) return NotFound(); await LoadBuildings(); return View(x); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditApartment(int id, Apartment x)
    { if (id != x.Id) return NotFound(); if (!ModelState.IsValid) { await LoadBuildings(); return View(x); } _db.Update(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> DeleteApartment(int id)
    { var x = await _db.Apartments.FindAsync(id); if (x != null) { _db.Apartments.Remove(x); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }

    public async Task<IActionResult> CreateRoom(int apartmentId) { await LoadApartments(); return View(new Room { ApartmentId = apartmentId }); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRoom(Room x)
    { if (!ModelState.IsValid) { await LoadApartments(); return View(x); } _db.Rooms.Add(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> EditRoom(int id) { var x = await _db.Rooms.FindAsync(id); if (x == null) return NotFound(); await LoadApartments(); return View(x); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRoom(int id, Room x)
    { if (id != x.Id) return NotFound(); if (!ModelState.IsValid) { await LoadApartments(); return View(x); } _db.Update(x); await _db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> DeleteRoom(int id)
    { var x = await _db.Rooms.FindAsync(id); if (x != null) { _db.Rooms.Remove(x); await _db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }

    private async Task LoadSites() => ViewBag.Sites = await _db.Sites.OrderBy(s => s.Name).ToListAsync();
    private async Task LoadBuildings() => ViewBag.Buildings = await _db.Buildings.Include(b => b.Site).OrderBy(b => b.Site!.Name).ThenBy(b => b.Number).ToListAsync();
    private async Task LoadApartments() => ViewBag.Apartments = await _db.Apartments.Include(a => a.Building).ThenInclude(b => b!.Site).OrderBy(a => a.Building!.Site!.Name).ThenBy(a => a.Building!.Number).ThenBy(a => a.Number).ToListAsync();
}
