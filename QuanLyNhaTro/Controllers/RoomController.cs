using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

public class RoomController : Controller
{
    private static readonly IReadOnlyDictionary<string, string> RoomStatuses =
        new Dictionary<string, string>
        {
            ["Available"] = "Trống",
            ["Occupied"] = "Đang thuê",
            ["Maintenance"] = "Bảo trì",
            ["Reserved"] = "Đã đặt trước"
        };

    private readonly ApplicationDbContext _context;

    public RoomController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var rooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        return View(rooms);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var room = await _context.Rooms
            .Include(item => item.Building)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.RoomId == id);

        if (room is null)
        {
            return NotFound();
        }

        return View(room);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateFormOptionsAsync();
        return View(new Room());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("BuildingId,RoomCode,MonthlyRent,MaxOccupants,Status,Description")] Room room)
    {
        ModelState.Remove(nameof(Room.Building));
        room.RoomCode = room.RoomCode?.Trim() ?? string.Empty;
        await ValidateRoomAsync(room);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(room.BuildingId, room.Status);
            return View(room);
        }

        _context.Rooms.Add(room);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Room.RoomCode), "Mã phòng đã tồn tại trong tòa nhà đã chọn.");
            await PopulateFormOptionsAsync(room.BuildingId, room.Status);
            return View(room);
        }

        TempData["SuccessMessage"] = "Đã thêm phòng thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var room = await _context.Rooms.FindAsync(id);

        if (room is null)
        {
            return NotFound();
        }

        await PopulateFormOptionsAsync(room.BuildingId, room.Status);
        return View(room);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("RoomId,BuildingId,RoomCode,MonthlyRent,MaxOccupants,Status,Description")] Room room)
    {
        ModelState.Remove(nameof(Room.Building));
        if (id != room.RoomId)
        {
            return NotFound();
        }

        room.RoomCode = room.RoomCode?.Trim() ?? string.Empty;
        await ValidateRoomAsync(room, room.RoomId);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(room.BuildingId, room.Status);
            return View(room);
        }

        var existingRoom = await _context.Rooms.FindAsync(id);

        if (existingRoom is null)
        {
            return NotFound();
        }

        existingRoom.BuildingId = room.BuildingId;
        existingRoom.RoomCode = room.RoomCode;
        existingRoom.MonthlyRent = room.MonthlyRent;
        existingRoom.MaxOccupants = room.MaxOccupants;
        existingRoom.Status = room.Status;
        existingRoom.Description = room.Description;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(Room.RoomCode), "Mã phòng đã tồn tại trong tòa nhà đã chọn.");
            await PopulateFormOptionsAsync(room.BuildingId, room.Status);
            return View(room);
        }

        TempData["SuccessMessage"] = "Đã cập nhật phòng thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var room = await _context.Rooms
            .Include(item => item.Building)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.RoomId == id);

        if (room is null)
        {
            return NotFound();
        }

        return View(room);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var room = await _context.Rooms.FindAsync(id);

        if (room is null)
        {
            return NotFound();
        }

        var hasContracts = await _context.RentalContracts
            .AsNoTracking()
            .AnyAsync(contract => contract.RoomId == id);

        if (hasContracts)
        {
            TempData["ErrorMessage"] = "Không thể xóa phòng vì phòng đang được tham chiếu bởi hợp đồng thuê.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            _context.Rooms.Remove(room);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Không thể xóa phòng vì dữ liệu vẫn đang được tham chiếu.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["SuccessMessage"] = "Đã xóa phòng thành công.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateRoomAsync(Room room, int? roomId = null)
    {
        if (string.IsNullOrWhiteSpace(room.RoomCode))
        {
            ModelState.AddModelError(nameof(Room.RoomCode), "Vui lòng nhập mã phòng.");
        }

        if (room.MonthlyRent <= 0)
        {
            ModelState.AddModelError(nameof(Room.MonthlyRent), "Giá thuê niêm yết phải lớn hơn 0.");
        }

        if (room.MaxOccupants < 1)
        {
            ModelState.AddModelError(nameof(Room.MaxOccupants), "Số người tối đa phải ít nhất là 1.");
        }

        if (string.IsNullOrWhiteSpace(room.Status) || !RoomStatuses.ContainsKey(room.Status))
        {
            ModelState.AddModelError(nameof(Room.Status), "Trạng thái phòng không hợp lệ.");
        }

        var buildingExists = await _context.Buildings
            .AsNoTracking()
            .AnyAsync(building => building.BuildingId == room.BuildingId);

        if (!buildingExists)
        {
            ModelState.AddModelError(nameof(Room.BuildingId), "Vui lòng chọn một tòa nhà tồn tại.");
        }

        if (!string.IsNullOrWhiteSpace(room.RoomCode) && buildingExists)
        {
            var duplicateExists = await _context.Rooms
                .AsNoTracking()
                .AnyAsync(item => item.BuildingId == room.BuildingId
                    && item.RoomCode == room.RoomCode
                    && (!roomId.HasValue || item.RoomId != roomId.Value));

            if (duplicateExists)
            {
                ModelState.AddModelError(nameof(Room.RoomCode), "Mã phòng đã tồn tại trong tòa nhà đã chọn.");
            }
        }

        if (room.RoomId > 0
            && room.Status == "Available"
            && await HasCurrentActiveContractAsync(room.RoomId))
        {
            ModelState.AddModelError(nameof(Room.Status), "Không thể đặt phòng là Trống vì phòng đang có hợp đồng hiệu lực.");
        }
    }

    private async Task<bool> HasCurrentActiveContractAsync(int roomId)
    {
        var today = DateTime.Today;

        return await _context.RentalContracts.AnyAsync(contract =>
            contract.RoomId == roomId
            && contract.Status == "Active"
            && contract.StartDate.Date <= today
            && contract.EndDate.Date >= today);
    }

    private async Task PopulateFormOptionsAsync(int? selectedBuildingId = null, string? selectedStatus = null)
    {
        var buildings = await _context.Buildings
            .AsNoTracking()
            .OrderBy(building => building.BuildingName)
            .ToListAsync();

        ViewBag.Buildings = new SelectList(
            buildings,
            nameof(Building.BuildingId),
            nameof(Building.BuildingName),
            selectedBuildingId);

        ViewBag.Statuses = RoomStatuses
            .Select(status => new SelectListItem
            {
                Value = status.Key,
                Text = status.Value,
                Selected = status.Key == selectedStatus
            })
            .ToList();
    }
}
