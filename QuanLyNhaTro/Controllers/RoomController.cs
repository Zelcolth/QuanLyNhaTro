using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class RoomController : Controller
{
    private static readonly IReadOnlyDictionary<string, string> RoomStatuses =
        new Dictionary<string, string>
        {
            ["Available"] = "Trống",
            ["Maintenance"] = "Bảo trì"
        };

    private readonly ApplicationDbContext _context;

    public RoomController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        await SynchronizeExpiredContractsAsync();

        var rooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        ViewBag.TotalRooms = rooms.Count;
        ViewBag.AvailableRooms = rooms.Count(room => room.Status == "Available");
        ViewBag.OccupiedRooms = rooms.Count(room => room.Status == "Occupied");
        ViewBag.MaintenanceRooms = rooms.Count(room => room.Status == "Maintenance");

        return View(rooms);
    }

    public async Task<IActionResult> Details(int? id)
    {
        await SynchronizeExpiredContractsAsync();

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
        await SynchronizeExpiredContractsAsync();

        if (id is null)
        {
            return NotFound();
        }

        var room = await _context.Rooms.FindAsync(id);

        if (room is null)
        {
            return NotFound();
        }

        var hasCurrentActiveContract = await HasCurrentActiveContractAsync(room.RoomId);
        if (hasCurrentActiveContract && room.Status != "Maintenance")
        {
            room.Status = "Occupied";
        }
        else if (!hasCurrentActiveContract && room.Status == "Occupied")
        {
            room.Status = "Available";
        }

        await PopulateFormOptionsAsync(room.BuildingId, room.Status, hasCurrentActiveContract);
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
            await PopulateFormOptionsAsync(room.BuildingId, room.Status, await HasCurrentActiveContractAsync(room.RoomId));
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
            await PopulateFormOptionsAsync(room.BuildingId, room.Status, await HasCurrentActiveContractAsync(room.RoomId));
            return View(room);
        }

        TempData["SuccessMessage"] = "Đã cập nhật phòng thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        await SynchronizeExpiredContractsAsync();

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

        var hasCurrentActiveContract = room.RoomId > 0
            && await HasCurrentActiveContractAsync(room.RoomId);
        var occupiedStatusAllowed = room.Status == "Occupied" && hasCurrentActiveContract;

        if (string.IsNullOrWhiteSpace(room.Status)
            || (!RoomStatuses.ContainsKey(room.Status) && !occupiedStatusAllowed))
        {
            ModelState.AddModelError(nameof(Room.Status), "Trạng thái phòng không hợp lệ. Trạng thái Đang thuê được xác định từ hợp đồng đang hiệu lực.");
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
            && hasCurrentActiveContract)
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

    private async Task SynchronizeExpiredContractsAsync()
    {
        var today = DateTime.Today;
        var expiredRoomIds = await _context.RentalContracts
            .Where(contract => contract.Status == "Active" && contract.EndDate.Date < today)
            .Select(contract => contract.RoomId)
            .Distinct()
            .ToListAsync();

        if (expiredRoomIds.Count == 0)
        {
            return;
        }

        await _context.RentalContracts
            .Where(contract => contract.Status == "Active" && contract.EndDate.Date < today)
            .ExecuteUpdateAsync(update => update.SetProperty(contract => contract.Status, "Expired"));

        foreach (var roomId in expiredRoomIds)
        {
            var room = await _context.Rooms.FindAsync(roomId);
            if (room is null || room.Status == "Maintenance")
            {
                continue;
            }

            var hasCurrentActiveContract = await _context.RentalContracts.AnyAsync(contract =>
                contract.RoomId == roomId
                && contract.Status == "Active"
                && contract.StartDate.Date <= today
                && contract.EndDate.Date >= today);

            room.Status = hasCurrentActiveContract ? "Occupied" : "Available";
        }

        await _context.SaveChangesAsync();
    }

    private async Task PopulateFormOptionsAsync(
        int? selectedBuildingId = null,
        string? selectedStatus = null,
        bool allowOccupiedStatus = false)
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

        var statusOptions = new List<SelectListItem>
        {
            new()
            {
                Value = "Available",
                Text = "Trống",
                Selected = selectedStatus == "Available"
            }
        };

        if (allowOccupiedStatus)
        {
            statusOptions.Add(new SelectListItem
            {
                Value = "Occupied",
                Text = "Đang thuê (theo hợp đồng đang hiệu lực)",
                Selected = selectedStatus == "Occupied"
            });
        }

        statusOptions.Add(new SelectListItem
        {
            Value = "Maintenance",
            Text = "Bảo trì",
            Selected = selectedStatus == "Maintenance"
        });

        ViewBag.Statuses = statusOptions;
    }
}
