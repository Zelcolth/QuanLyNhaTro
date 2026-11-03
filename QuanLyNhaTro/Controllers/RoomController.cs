using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;
using QuanLyNhaTro.ViewModels;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class RoomController : Controller
{
    private static readonly IReadOnlyDictionary<string, string> RoomStatuses =
        new Dictionary<string, string>
        {
            ["Available"] = "Trống",
            ["Maintenance"] = "Bảo trì",
            ["Cleaning"] = "Chờ xử lý"
        };

    private readonly ApplicationDbContext _context;

    public RoomController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(int? openOccupants = null)
    {
        await SynchronizeExpiredContractsAsync();

        var rooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        var today = DateTime.Today;
        var currentBillingMonth = new DateTime(today.Year, today.Month, 1);
        var invoicedRoomIds = await _context.Invoices
            .Where(invoice => invoice.BillingMonth == currentBillingMonth)
            .Select(invoice => invoice.Contract.RoomId)
            .Distinct()
            .ToListAsync();

        var currentBillingPeriodEnd = currentBillingMonth.AddMonths(1).AddDays(-1);
        var billableRoomIds = await _context.RentalContracts
            .Where(contract =>
                contract.Status != "Draft"
                && contract.Status != "Cancelled"
                && contract.StartDate.Date <= currentBillingMonth
                && (
                    (contract.ActualEndDate == null && contract.EndDate.Date >= currentBillingPeriodEnd)
                    || (contract.ActualEndDate != null && contract.ActualEndDate.Value.Date >= currentBillingPeriodEnd)
                ))
            .Select(contract => contract.RoomId)
            .Distinct()
            .ToListAsync();

        ViewBag.TotalRooms = rooms.Count;
        ViewBag.AvailableRooms = rooms.Count(room => room.Status == "Available");
        ViewBag.OccupiedRooms = rooms.Count(room => room.Status == "Occupied");
        ViewBag.MaintenanceRooms = rooms.Count(room => room.Status == "Maintenance");
        ViewBag.CleaningRooms = rooms.Count(room => room.Status == "Cleaning");
        var activeContractNotices = await _context.RentalContracts
            .AsNoTracking()
            .Where(contract => contract.Status == "Active"
                && contract.StartDate.Date <= today
                && contract.EndDate.Date >= today)
            .Select(contract => new
            {
                contract.RoomId,
                contract.ExpectedMoveOutDate,
                contract.EndDate
            })
            .ToListAsync();

        var moveOutDatesByRoom = activeContractNotices
            .Where(contract => contract.ExpectedMoveOutDate.HasValue)
            .GroupBy(contract => contract.RoomId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(contract => contract.ExpectedMoveOutDate).First().ExpectedMoveOutDate!.Value);

        const int contractExpiryWarningDays = 30;
        var expiringDatesByRoom = activeContractNotices
            .Where(contract => contract.EndDate.Date >= today
                && contract.EndDate.Date <= today.AddDays(contractExpiryWarningDays))
            .GroupBy(contract => contract.RoomId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(contract => contract.EndDate).First().EndDate);

        ViewBag.MoveOutDatesByRoom = moveOutDatesByRoom;
        ViewBag.ExpiringDatesByRoom = expiringDatesByRoom;
        ViewBag.ContractExpiryWarningDays = contractExpiryWarningDays;
        ViewBag.InvoicedRoomIds = invoicedRoomIds.ToHashSet();
        ViewBag.BillableRoomIds = billableRoomIds.ToHashSet();
        ViewBag.InvoiceBillingMonthLabel = currentBillingMonth.ToString("MM/yyyy");
        ViewBag.OpenOccupantsRoomId = openOccupants;

        return View(rooms);
    }


    [HttpGet]
    public async Task<IActionResult> Occupants(int id)
    {
        await SynchronizeExpiredContractsAsync();

        var room = await _context.Rooms
            .Include(item => item.Building)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.RoomId == id);

        if (room is null)
        {
            return NotFound();
        }

        var today = DateTime.Today;
        var contract = await _context.RentalContracts
            .Where(item => item.RoomId == id
                && item.Status == "Active"
                && item.StartDate.Date <= today
                && item.EndDate.Date >= today)
            .Include(item => item.ContractTenants)
                .ThenInclude(assignment => assignment.Tenant)
            .AsNoTracking()
            .OrderByDescending(item => item.StartDate)
            .FirstOrDefaultAsync();

        var linkedTenantIds = contract?.ContractTenants
            .Select(assignment => assignment.TenantId)
            .ToHashSet() ?? new HashSet<int>();

        var availableTenants = contract is null
            ? new List<TenantProfile>()
            : await _context.TenantProfiles
                .AsNoTracking()
                .Where(tenant => !linkedTenantIds.Contains(tenant.TenantId))
                .OrderBy(tenant => tenant.FullName)
                .ToListAsync();

        var model = new RoomOccupantsViewModel
        {
            RoomId = room.RoomId,
            RoomLabel = $"{room.Building.BuildingName} - {room.RoomCode}",
            MaxOccupants = room.MaxOccupants,
            ContractId = contract?.ContractId,
            ContractCode = contract?.ContractCode,
            ActiveOccupants = contract?.ContractTenants
                .Where(assignment => assignment.IsActive)
                .OrderByDescending(assignment => assignment.IsPrimaryTenant)
                .ThenBy(assignment => assignment.Tenant.FullName)
                .ToList() ?? new List<ContractTenant>(),
            FormerOccupants = contract?.ContractTenants
                .Where(assignment => !assignment.IsActive)
                .OrderByDescending(assignment => assignment.LeftAt)
                .ThenBy(assignment => assignment.Tenant.FullName)
                .ToList() ?? new List<ContractTenant>(),
            AvailableTenants = availableTenants
        };

        return PartialView("_OccupantsModal", model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOccupant(
        int id,
        int tenantId,
        string? relationshipToPrimaryTenant)
    {
        await SynchronizeExpiredContractsAsync();

        var room = await _context.Rooms
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.RoomId == id);

        if (room is null)
        {
            return NotFound();
        }

        var contract = await GetCurrentActiveContractForRoomAsync(id);
        if (contract is null)
        {
            TempData["ErrorMessage"] = "Phòng hiện không có hợp đồng đang hiệu lực.";
            return RedirectToAction(nameof(Index), new { openOccupants = id });
        }

        if (contract.ContractTenants.Count(assignment => assignment.IsActive) >= room.MaxOccupants)
        {
            TempData["ErrorMessage"] = $"Phòng đã đạt giới hạn {room.MaxOccupants} người.";
            return RedirectToAction(nameof(Index), new { openOccupants = id });
        }

        if (contract.ContractTenants.Any(assignment => assignment.TenantId == tenantId))
        {
            TempData["ErrorMessage"] = "Người này đã hoặc từng được ghi nhận trong hợp đồng.";
            return RedirectToAction(nameof(Index), new { openOccupants = id });
        }

        if (!await _context.TenantProfiles.AnyAsync(tenant => tenant.TenantId == tenantId))
        {
            TempData["ErrorMessage"] = "Hồ sơ người thuê không tồn tại. Vui lòng tải lại trang.";
            return RedirectToAction(nameof(Index), new { openOccupants = id });
        }

        if (relationshipToPrimaryTenant is not null
            && relationshipToPrimaryTenant.Trim().Length > 100)
        {
            TempData["ErrorMessage"] = "Quan hệ với người đứng tên không được vượt quá 100 ký tự.";
            return RedirectToAction(nameof(Index), new { openOccupants = id });
        }

        _context.ContractTenants.Add(new ContractTenant
        {
            ContractId = contract.ContractId,
            TenantId = tenantId,
            IsPrimaryTenant = false,
            RelationshipToPrimaryTenant = NormalizeOptionalText(relationshipToPrimaryTenant),
            IsActive = true,
            JoinedAt = DateTime.Now
        });

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã thêm người ở phụ.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Không thể thêm người ở phụ. Vui lòng tải lại trang và thử lại.";
        }

        return RedirectToAction(nameof(Index), new { openOccupants = id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkOccupantLeft(int id, int tenantId)
    {
        await SynchronizeExpiredContractsAsync();

        var roomExists = await _context.Rooms.AnyAsync(item => item.RoomId == id);
        if (!roomExists)
        {
            return NotFound();
        }

        var contract = await GetCurrentActiveContractForRoomAsync(id);
        if (contract is null)
        {
            TempData["ErrorMessage"] = "Phòng hiện không có hợp đồng đang hiệu lực.";
            return RedirectToAction(nameof(Index), new { openOccupants = id });
        }

        var assignment = contract.ContractTenants.FirstOrDefault(item =>
            item.TenantId == tenantId && item.IsActive && !item.IsPrimaryTenant);

        if (assignment is null)
        {
            TempData["ErrorMessage"] = "Chỉ có thể ghi nhận người ở phụ đã rời đi.";
            return RedirectToAction(nameof(Index), new { openOccupants = id });
        }

        assignment.IsActive = false;
        assignment.LeftAt = DateTime.Now;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã ghi nhận người ở phụ rời khỏi phòng.";
        return RedirectToAction(nameof(Index), new { openOccupants = id });
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

        var today = DateTime.Today;
        var activeContract = await _context.RentalContracts
            .Where(contract => contract.RoomId == room.RoomId
                && contract.Status == "Active"
                && contract.StartDate.Date <= today
                && contract.EndDate.Date >= today)
            .Include(contract => contract.ContractTenants)
                .ThenInclude(assignment => assignment.Tenant)
            .AsNoTracking()
            .OrderByDescending(contract => contract.StartDate)
            .FirstOrDefaultAsync();

        return View(new RoomDetailsViewModel
        {
            Room = room,
            ActiveContract = activeContract
        });
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
        LocalizeRoomBindingErrors();
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
        LocalizeRoomBindingErrors();
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


    private async Task<RentalContract?> GetCurrentActiveContractForRoomAsync(int roomId)
    {
        var today = DateTime.Today;

        return await _context.RentalContracts
            .Include(contract => contract.ContractTenants)
            .Where(contract => contract.RoomId == roomId
                && contract.Status == "Active"
                && contract.StartDate.Date <= today
                && contract.EndDate.Date >= today)
            .OrderByDescending(contract => contract.StartDate)
            .FirstOrDefaultAsync();
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalizedValue = value?.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }

    private async Task ValidateRoomAsync(Room room, int? roomId = null)
    {
        var hasCurrentActiveContract = room.RoomId > 0
            && await HasCurrentActiveContractAsync(room.RoomId);
        var occupiedStatusAllowed = room.Status == "Occupied" && hasCurrentActiveContract;

        if (!string.IsNullOrWhiteSpace(room.Status)
            && !RoomStatuses.ContainsKey(room.Status)
            && !occupiedStatusAllowed)
        {
            ModelState.AddModelError(nameof(Room.Status), "Trạng thái phòng không hợp lệ. Trạng thái Đang thuê được xác định từ hợp đồng đang hiệu lực.");
        }

        var buildingExists = room.BuildingId > 0
            && await _context.Buildings
                .AsNoTracking()
                .AnyAsync(building => building.BuildingId == room.BuildingId);

        if (room.BuildingId > 0 && !buildingExists)
        {
            ModelState.AddModelError(nameof(Room.BuildingId), "Tòa nhà đã chọn không tồn tại.");
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

    private void LocalizeRoomBindingErrors()
    {
        LocalizeBindingError(
            nameof(Room.BuildingId),
            "Vui lòng chọn tòa nhà.",
            "Tòa nhà đã chọn không hợp lệ.");
        LocalizeBindingError(
            nameof(Room.MonthlyRent),
            "Vui lòng nhập giá thuê niêm yết.",
            "Giá thuê niêm yết phải là số hợp lệ.");
        LocalizeBindingError(
            nameof(Room.MaxOccupants),
            "Vui lòng nhập số người tối đa.",
            "Số người tối đa phải là số nguyên hợp lệ.");
    }

    private void LocalizeBindingError(string field, string emptyMessage, string invalidMessage)
    {
        if (!ModelState.TryGetValue(field, out var entry)
            || !entry.Errors.Any(error => error.Exception is not null))
        {
            return;
        }

        var rawValue = entry.RawValue;
        var attemptedValue = entry.AttemptedValue;
        ModelState.Remove(field);
        ModelState.SetModelValue(field, rawValue, attemptedValue);
        ModelState.AddModelError(
            field,
            string.IsNullOrWhiteSpace(attemptedValue) ? emptyMessage : invalidMessage);
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
            if (room is null || room.Status is "Maintenance" or "Cleaning")
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
        statusOptions.Add(new SelectListItem
        {
            Value = "Cleaning",
            Text = "Chờ xử lý",
            Selected = selectedStatus == "Cleaning"
        });

        ViewBag.Statuses = statusOptions;
    }
}
