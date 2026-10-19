using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

public class RentalContractController : Controller
{
    private static readonly IReadOnlyDictionary<string, string> ContractStatuses =
        new Dictionary<string, string>
        {
            ["Draft"] = "Bản nháp",
            ["Active"] = "Đang hiệu lực",
            ["Completed"] = "Đã hoàn thành",
            ["Cancelled"] = "Đã hủy"
        };

    private readonly ApplicationDbContext _context;

    public RentalContractController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var contracts = await _context.RentalContracts
            .Include(contract => contract.Room)
                .ThenInclude(room => room.Building)
            .Include(contract => contract.Tenant)
            .AsNoTracking()
            .OrderByDescending(contract => contract.StartDate)
            .ThenBy(contract => contract.ContractCode)
            .ToListAsync();

        return View(contracts);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var contract = await _context.RentalContracts
            .Include(item => item.Room)
                .ThenInclude(room => room.Building)
            .Include(item => item.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ContractId == id);

        if (contract is null)
        {
            return NotFound();
        }

        return View(contract);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateFormOptionsAsync();
        return View(new RentalContract
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1),
            Status = "Draft"
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("RoomId,TenantId,ContractCode,StartDate,EndDate,MonthlyRent,Deposit,Status,Terms")] RentalContract contract)
    {
        NormalizeContract(contract);
        RemoveUnpostedModelStateEntries();
        await ValidateContractAsync(contract);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(contract.RoomId, contract.TenantId, contract.Status);
            return View(contract);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            _context.RentalContracts.Add(contract);
            await _context.SaveChangesAsync();

            await SynchronizeRoomStatusAsync(contract.RoomId);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            ModelState.AddModelError(nameof(RentalContract.ContractCode), "Mã hợp đồng đã tồn tại hoặc dữ liệu tham chiếu không hợp lệ.");
            await PopulateFormOptionsAsync(contract.RoomId, contract.TenantId, contract.Status);
            return View(contract);
        }

        TempData["SuccessMessage"] = "Đã thêm hợp đồng thuê thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var contract = await _context.RentalContracts.FindAsync(id);

        if (contract is null)
        {
            return NotFound();
        }

        await PopulateFormOptionsAsync(contract.RoomId, contract.TenantId, contract.Status);
        return View(contract);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("ContractId,RoomId,TenantId,ContractCode,StartDate,EndDate,MonthlyRent,Deposit,Status,Terms")] RentalContract contract)
    {
        if (id != contract.ContractId)
        {
            return NotFound();
        }

        NormalizeContract(contract);
        RemoveUnpostedModelStateEntries();
        await ValidateContractAsync(contract, contract.ContractId);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(contract.RoomId, contract.TenantId, contract.Status);
            return View(contract);
        }

        var existingContract = await _context.RentalContracts.FindAsync(id);

        if (existingContract is null)
        {
            return NotFound();
        }

        var previousRoomId = existingContract.RoomId;
        existingContract.RoomId = contract.RoomId;
        existingContract.TenantId = contract.TenantId;
        existingContract.ContractCode = contract.ContractCode;
        existingContract.StartDate = contract.StartDate;
        existingContract.EndDate = contract.EndDate;
        existingContract.MonthlyRent = contract.MonthlyRent;
        existingContract.Deposit = contract.Deposit;
        existingContract.Status = contract.Status;
        existingContract.Terms = contract.Terms;

        try
        {
            await _context.SaveChangesAsync();
            await SynchronizeRoomStatusAsync(previousRoomId);
            if (previousRoomId != contract.RoomId)
            {
                await SynchronizeRoomStatusAsync(contract.RoomId);
            }

            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await ContractExistsAsync(id))
            {
                return NotFound();
            }

            ModelState.AddModelError(string.Empty, "Dữ liệu hợp đồng đã được thay đổi bởi người khác. Vui lòng tải lại trang và thử lại.");
            await PopulateFormOptionsAsync(contract.RoomId, contract.TenantId, contract.Status);
            return View(contract);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(RentalContract.ContractCode), "Mã hợp đồng đã tồn tại hoặc dữ liệu tham chiếu không hợp lệ.");
            await PopulateFormOptionsAsync(contract.RoomId, contract.TenantId, contract.Status);
            return View(contract);
        }

        TempData["SuccessMessage"] = "Đã cập nhật hợp đồng thuê thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndContract(int id)
    {
        var contract = await _context.RentalContracts.FindAsync(id);

        if (contract is null)
        {
            return NotFound();
        }

        if (contract.Status != "Active")
        {
            TempData["ErrorMessage"] = "Chỉ có thể kết thúc hợp đồng đang hiệu lực.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var room = await _context.Rooms.FindAsync(contract.RoomId);

        if (room is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phòng liên kết với hợp đồng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        contract.Status = "Completed";
        contract.EndDate = DateTime.Now;

        // Sau này có thể tính khấu trừ cọc tại đây:
        // RefundAmount = contract.Deposit - (UnpaidInvoices + Damages);
        // Cần bổ sung bảng hóa đơn/thiệt hại và migration khi được duyệt.

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await _context.SaveChangesAsync();

            // Tính lại trạng thái phòng từ các hợp đồng còn hiệu lực,
            // đồng thời giữ nguyên trạng thái Maintenance hoặc Reserved.
            await SynchronizeRoomStatusAsync(contract.RoomId);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            TempData["ErrorMessage"] = "Không thể kết thúc hợp đồng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Đã kết thúc hợp đồng thuê.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var contract = await _context.RentalContracts
            .Include(item => item.Room)
                .ThenInclude(room => room.Building)
            .Include(item => item.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ContractId == id);

        if (contract is null)
        {
            return NotFound();
        }

        return View(contract);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var contract = await _context.RentalContracts.FindAsync(id);

        if (contract is null)
        {
            return NotFound();
        }

        if (contract.Status is not ("Draft" or "Cancelled"))
        {
            TempData["ErrorMessage"] = "Chỉ có thể xóa hợp đồng ở trạng thái Bản nháp hoặc Đã hủy.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        var roomId = contract.RoomId;
        _context.RentalContracts.Remove(contract);

        try
        {
            await _context.SaveChangesAsync();
            await SynchronizeRoomStatusAsync(roomId);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Không thể xóa hợp đồng vì dữ liệu vẫn đang được tham chiếu.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["SuccessMessage"] = "Đã xóa hợp đồng thuê thành công.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateContractAsync(RentalContract contract, int? contractId = null)
    {
        var startDate = contract.StartDate.Date;
        var endDate = contract.EndDate.Date;
        var today = DateTime.Today;

        ModelState.Remove(nameof(RentalContract.ContractCode));
        ModelState.Remove(nameof(RentalContract.Status));

        if (string.IsNullOrWhiteSpace(contract.ContractCode))
        {
            ModelState.AddModelError(nameof(RentalContract.ContractCode), "Vui lòng nhập mã hợp đồng.");
        }
        else if (await _context.RentalContracts.AnyAsync(item =>
            item.ContractCode == contract.ContractCode
            && (!contractId.HasValue || item.ContractId != contractId.Value)))
        {
            ModelState.AddModelError(nameof(RentalContract.ContractCode), "Mã hợp đồng đã tồn tại.");
        }

        if (contract.StartDate == default)
        {
            ModelState.AddModelError(nameof(RentalContract.StartDate), "Vui lòng nhập ngày bắt đầu.");
        }

        if (contract.EndDate == default)
        {
            ModelState.AddModelError(nameof(RentalContract.EndDate), "Vui lòng nhập ngày kết thúc.");
        }

        if (contract.StartDate != default
            && contract.EndDate != default)
        {
            // Hợp đồng đã hoàn thành có thể được kết thúc ngay trong ngày bắt đầu.
            // Các trạng thái khác vẫn yêu cầu ngày kết thúc sau ngày bắt đầu.
            var invalidDateRange = contract.Status == "Completed"
                ? startDate > endDate
                : startDate >= endDate;

            if (invalidDateRange)
            {
                ModelState.AddModelError(
                    nameof(RentalContract.EndDate),
                    contract.Status == "Completed"
                        ? "Ngày kết thúc không được trước ngày bắt đầu."
                        : "Ngày bắt đầu phải sớm hơn ngày kết thúc.");
            }
        }

        if (contract.MonthlyRent < 0)
        {
            ModelState.AddModelError(nameof(RentalContract.MonthlyRent), "Tiền thuê không được âm.");
        }

        if (contract.Deposit < 0)
        {
            ModelState.AddModelError(nameof(RentalContract.Deposit), "Tiền đặt cọc không được âm.");
        }

        if (!ContractStatuses.ContainsKey(contract.Status ?? string.Empty))
        {
            ModelState.AddModelError(nameof(RentalContract.Status), "Trạng thái hợp đồng không hợp lệ.");
        }

        var roomExists = await _context.Rooms
            .AsNoTracking()
            .AnyAsync(room => room.RoomId == contract.RoomId);

        if (!roomExists)
        {
            ModelState.AddModelError(nameof(RentalContract.RoomId), "Vui lòng chọn một phòng tồn tại.");
        }

        var tenantExists = await _context.TenantProfiles
            .AsNoTracking()
            .AnyAsync(tenant => tenant.TenantId == contract.TenantId);

        if (!tenantExists)
        {
            ModelState.AddModelError(nameof(RentalContract.TenantId), "Vui lòng chọn một người thuê tồn tại.");
        }

        if (contract.Status == "Active"
            && (contract.StartDate == default
                || contract.EndDate == default
                || today < startDate
                || today > endDate))
        {
            ModelState.AddModelError(nameof(RentalContract.Status), "Chỉ được chọn Đang hiệu lực khi ngày hiện tại nằm trong thời hạn hợp đồng.");
        }

        if (contract.Status == "Active"
            && roomExists
            && contract.StartDate != default
            && contract.EndDate != default
            && startDate < endDate
            && await HasOverlappingActiveContractAsync(contract, contractId))
        {
            ModelState.AddModelError(nameof(RentalContract.RoomId), "Phòng đã có hợp đồng đang hiệu lực bị chồng lấn thời gian.");
        }
    }

    private async Task<bool> HasOverlappingActiveContractAsync(RentalContract contract, int? contractId)
    {
        var startDate = contract.StartDate.Date;
        var endDate = contract.EndDate.Date;

        return await _context.RentalContracts.AnyAsync(item =>
            item.RoomId == contract.RoomId
            && item.Status == "Active"
            && item.StartDate.Date <= endDate
            && item.EndDate.Date >= startDate
            && (!contractId.HasValue || item.ContractId != contractId.Value));
    }

    private async Task SynchronizeRoomStatusAsync(int roomId)
    {
        var room = await _context.Rooms.FindAsync(roomId);

        if (room is null)
        {
            return;
        }

        if (room.Status is "Maintenance" or "Reserved")
        {
            return;
        }

        var today = DateTime.Today;
        var hasCurrentActiveContract = await _context.RentalContracts.AnyAsync(contract =>
            contract.RoomId == roomId
            && contract.Status == "Active"
            && contract.StartDate.Date <= today
            && contract.EndDate.Date >= today);

        if (hasCurrentActiveContract)
        {
            room.Status = "Occupied";
        }
        else
        {
            room.Status = "Available";
        }
    }

    private async Task PopulateFormOptionsAsync(
        int? selectedRoomId = null,
        int? selectedTenantId = null,
        string? selectedStatus = null)
    {
        var rooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        var tenants = await _context.TenantProfiles
            .AsNoTracking()
            .OrderBy(tenant => tenant.FullName)
            .ToListAsync();

        ViewBag.Rooms = rooms
            .Select(room => new SelectListItem
            {
                Value = room.RoomId.ToString(),
                Text = $"{room.Building.BuildingName} - Phòng {room.RoomCode}",
                Selected = room.RoomId == selectedRoomId
            })
            .ToList();

        ViewBag.Tenants = new SelectList(
            tenants,
            nameof(TenantProfile.TenantId),
            nameof(TenantProfile.FullName),
            selectedTenantId);

        ViewBag.Statuses = ContractStatuses
            .Select(status => new SelectListItem
            {
                Value = status.Key,
                Text = status.Value,
                Selected = status.Key == selectedStatus
            })
            .ToList();
    }

    private void RemoveUnpostedModelStateEntries()
    {
        ModelState.Remove(nameof(RentalContract.Room));
        ModelState.Remove(nameof(RentalContract.Tenant));
    }

    private static void NormalizeContract(RentalContract contract)
    {
        contract.ContractCode = contract.ContractCode?.Trim() ?? string.Empty;
        contract.Status = contract.Status?.Trim() ?? string.Empty;
        contract.Terms = string.IsNullOrWhiteSpace(contract.Terms)
            ? null
            : contract.Terms.Trim();
    }

    private async Task<bool> ContractExistsAsync(int id)
    {
        return await _context.RentalContracts.AnyAsync(contract => contract.ContractId == id);
    }
}
