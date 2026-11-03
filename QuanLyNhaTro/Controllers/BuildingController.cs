using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class BuildingController : Controller
{
    private readonly ApplicationDbContext _context;

    public BuildingController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var buildings = await _context.Buildings
            .Include(building => building.Rooms)
            .AsNoTracking()
            .OrderBy(building => building.BuildingName)
            .ToListAsync();

        ViewBag.TotalBuildings = buildings.Count;
        ViewBag.TotalRooms = buildings.Sum(building => building.Rooms.Count);

        return View(buildings);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var building = await _context.Buildings
            .Include(item => item.Rooms)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BuildingId == id);

        if (building is null)
        {
            return NotFound();
        }

        return View(building);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Building building)
    {
        if (!ModelState.IsValid)
        {
            return View(building);
        }

        _context.Buildings.Add(building);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã thêm tòa nhà thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var building = await _context.Buildings.FindAsync(id);

        if (building is null)
        {
            return NotFound();
        }

        return View(building);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Building building)
    {
        if (id != building.BuildingId)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(building);
        }

        try
        {
            _context.Update(building);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await BuildingExistsAsync(building.BuildingId))
            {
                return NotFound();
            }

            ModelState.AddModelError(string.Empty, "Dữ liệu tòa nhà đã được thay đổi bởi người khác. Vui lòng tải lại trang và thử lại.");
            return View(building);
        }

        TempData["SuccessMessage"] = "Đã cập nhật tòa nhà thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var building = await _context.Buildings
            .Include(item => item.Rooms)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.BuildingId == id);

        if (building is null)
        {
            return NotFound();
        }

        return View(building);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var building = await _context.Buildings.FindAsync(id);

        if (building is null)
        {
            return NotFound();
        }

        var hasRooms = await _context.Rooms
            .AsNoTracking()
            .AnyAsync(room => room.BuildingId == id);

        if (hasRooms)
        {
            TempData["ErrorMessage"] = "Không thể xóa tòa nhà vì tòa nhà vẫn còn phòng. Vui lòng xóa hoặc chuyển các phòng trước khi xóa tòa nhà.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            _context.Buildings.Remove(building);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Không thể xóa tòa nhà vì dữ liệu vẫn đang được tham chiếu. Vui lòng kiểm tra lại các phòng và hợp đồng liên quan.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["SuccessMessage"] = "Đã xóa tòa nhà thành công.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> BuildingExistsAsync(int id)
    {
        return await _context.Buildings.AnyAsync(building => building.BuildingId == id);
    }
}
