using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class RentalContractController : Controller
{
    private readonly ApplicationDbContext _context;

    public RentalContractController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        await SynchronizeExpiredContractsAsync();

        var contracts = await _context.RentalContracts
            .Include(contract => contract.Room)
                .ThenInclude(room => room.Building)
            .Include(contract => contract.ContractTenants)
                .ThenInclude(contractTenant => contractTenant.Tenant)
            .AsNoTracking()
            .OrderByDescending(contract => contract.StartDate)
            .ThenBy(contract => contract.ContractCode)
            .ToListAsync();

        ViewBag.ActiveContracts = contracts.Count(contract => contract.Status == "Active");
        ViewBag.DraftContracts = contracts.Count(contract => contract.Status == "Draft");
        ViewBag.ExpiredContracts = contracts.Count(contract => contract.Status == "Expired");
        ViewBag.TerminatedContracts = contracts.Count(contract => contract.Status == "Terminated");

        return View(contracts);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        await SynchronizeExpiredContractsAsync();

        var contract = await _context.RentalContracts
            .Include(item => item.Room)
                .ThenInclude(room => room.Building)
            .Include(item => item.ContractTenants)
                .ThenInclude(contractTenant => contractTenant.Tenant)
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
        await SynchronizeExpiredContractsAsync();

        var model = new RentalContractFormViewModel
        {
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(1)
        };

        await PopulateFormOptionsAsync(model, preserveTenantOptions: false);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RentalContractFormViewModel model, bool confirmOverlap = false)
    {
        await SynchronizeExpiredContractsAsync();
        NormalizeContract(model);
        await ValidateContractAsync(model);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
            return View(model);
        }

        var hasOverlappingContract = await HasOverlappingActiveContractAsync(
            model.RoomId,
            model.StartDate,
            model.EndDate,
            null);

        if (hasOverlappingContract && !confirmOverlap)
        {
            await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
            ViewBag.OverlapWarning = "Phòng đã có hợp đồng đang hiệu lực bị chồng lấn thời gian với khoảng ngày đã chọn. Bạn vẫn có thể lưu bản nháp để hoàn thiện sau, nhưng hệ thống sẽ không cho kích hoạt khi xung đột còn tồn tại.";
            return View(model);
        }

        var contract = MapContract(model);
        AddSelectedTenants(contract, model);

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

            ModelState.AddModelError(string.Empty, "Không thể lưu hợp đồng. Vui lòng kiểm tra mã hợp đồng và thông tin người thuê.");
            await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã tạo hợp đồng ở trạng thái Bản nháp.";
        return RedirectToAction(nameof(Details), new { id = contract.ContractId });
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        await SynchronizeExpiredContractsAsync();

        var contract = await _context.RentalContracts
            .Include(item => item.ContractTenants)
            .FirstOrDefaultAsync(item => item.ContractId == id);

        if (contract is null)
        {
            return NotFound();
        }

        var model = MapForm(contract);
        await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RentalContractFormViewModel model, bool confirmOverlap = false)
    {
        if (id != model.ContractId)
        {
            return NotFound();
        }

        await SynchronizeExpiredContractsAsync();

        var existingContract = await _context.RentalContracts
            .Include(item => item.ContractTenants)
            .FirstOrDefaultAsync(item => item.ContractId == id);

        if (existingContract is null)
        {
            return NotFound();
        }

        NormalizeContract(model);
        await ValidateContractAsync(model, model.ContractId, existingContract.Status);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
            return View(model);
        }

        // Warn before saving a draft, but keep the existing hard validation for active contracts.
        if (existingContract.Status == "Draft"
            && await HasOverlappingActiveContractAsync(
                model.RoomId,
                model.StartDate,
                model.EndDate,
                model.ContractId)
            && !confirmOverlap)
        {
            await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
            ViewBag.OverlapWarning = "Phòng đã có hợp đồng đang hiệu lực bị chồng lấn thời gian với khoảng ngày đã chọn. Bạn vẫn có thể lưu bản nháp để hoàn thiện sau, nhưng hệ thống sẽ không cho kích hoạt khi xung đột còn tồn tại.";
            return View(model);
        }

        var previousRoomId = existingContract.RoomId;
        existingContract.RoomId = model.RoomId;
        existingContract.ContractCode = model.ContractCode;
        existingContract.StartDate = model.StartDate;
        existingContract.EndDate = model.EndDate;
        existingContract.MonthlyRent = model.MonthlyRent;
        existingContract.Deposit = model.Deposit;
        existingContract.Terms = model.Terms;
        // Status is deliberately preserved: status transitions use dedicated actions.
        SynchronizeTenantAssignments(existingContract, model);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await _context.SaveChangesAsync();

            await SynchronizeRoomStatusAsync(previousRoomId);
            if (previousRoomId != model.RoomId)
            {
                await SynchronizeRoomStatusAsync(model.RoomId);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync();

            if (!await ContractExistsAsync(id))
            {
                return NotFound();
            }

            ModelState.AddModelError(string.Empty, "Dữ liệu hợp đồng đã được thay đổi bởi người khác. Vui lòng tải lại trang và thử lại.");
            await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
            return View(model);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            ModelState.AddModelError(string.Empty, "Không thể cập nhật hợp đồng. Vui lòng kiểm tra mã hợp đồng và danh sách người thuê.");
            await PopulateFormOptionsAsync(model, preserveTenantOptions: true);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật hợp đồng thuê thành công.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id)
    {
        await SynchronizeExpiredContractsAsync();

        var contract = await _context.RentalContracts
            .FirstOrDefaultAsync(item => item.ContractId == id);

        if (contract is null)
        {
            return NotFound();
        }

        if (contract.Status != "Draft")
        {
            TempData["ErrorMessage"] = "Chỉ có thể kích hoạt hợp đồng ở trạng thái Bản nháp.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var room = await _context.Rooms.AsNoTracking()
            .FirstOrDefaultAsync(item => item.RoomId == contract.RoomId);

        if (room is null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy phòng của hợp đồng.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (room.Status == "Maintenance")
        {
            TempData["ErrorMessage"] = "Không thể kích hoạt hợp đồng khi phòng đang bảo trì.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var today = DateTime.Today;
        if (contract.StartDate.Date > today
            || contract.EndDate.Date < today
            || contract.StartDate.Date >= contract.EndDate.Date)
        {
            TempData["ErrorMessage"] = "Chỉ có thể kích hoạt khi ngày hiện tại nằm trong thời hạn hợp đồng. Nếu hợp đồng bắt đầu trong tương lai, hãy kích hoạt vào ngày bắt đầu.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (await HasOverlappingActiveContractAsync(
            contract.RoomId,
            contract.StartDate,
            contract.EndDate,
            contract.ContractId))
        {
            TempData["ErrorMessage"] = "Phòng đã có hợp đồng đang hiệu lực bị chồng lấn thời gian.";
            return RedirectToAction(nameof(Details), new { id });
        }

        contract.Status = "Active";

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await _context.SaveChangesAsync();
            await SynchronizeRoomStatusAsync(contract.RoomId);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            TempData["ErrorMessage"] = "Không thể kích hoạt hợp đồng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Đã kích hoạt hợp đồng thuê.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EndContract(int id)
    {
        await SynchronizeExpiredContractsAsync();

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

        contract.Status = "Terminated";
        contract.ActualEndDate = DateTime.Now;
        // Keep EndDate as the contractual end date; ActualEndDate records early termination.

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await _context.SaveChangesAsync();
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

        TempData["SuccessMessage"] = "Đã ghi nhận ngày chấm dứt thực tế của hợp đồng.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        await SynchronizeExpiredContractsAsync();

        var contract = await _context.RentalContracts
            .Include(item => item.Room)
                .ThenInclude(room => room.Building)
            .Include(item => item.ContractTenants)
                .ThenInclude(contractTenant => contractTenant.Tenant)
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
        await SynchronizeExpiredContractsAsync();

        var contract = await _context.RentalContracts.FindAsync(id);

        if (contract is null)
        {
            return NotFound();
        }

        // Cancelled is accepted only for legacy records created by the previous version.
        if (contract.Status is not ("Draft" or "Cancelled"))
        {
            TempData["ErrorMessage"] = "Chỉ có thể xóa hợp đồng Bản nháp. Hợp đồng đã có hiệu lực hoặc đã kết thúc phải được lưu lại để tra cứu.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        var roomId = contract.RoomId;
        _context.RentalContracts.Remove(contract);

        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            await _context.SaveChangesAsync();
            await SynchronizeRoomStatusAsync(roomId);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();

            TempData["ErrorMessage"] = "Không thể xóa hợp đồng vì dữ liệu vẫn đang được tham chiếu.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["SuccessMessage"] = "Đã xóa hợp đồng bản nháp.";
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidateContractAsync(
        RentalContractFormViewModel model,
        int? contractId = null,
        string contractStatus = "Draft")
    {
        var startDate = model.StartDate.Date;
        var endDate = model.EndDate.Date;
        var today = DateTime.Today;

        ModelState.Remove(nameof(RentalContractFormViewModel.ContractCode));

        if (string.IsNullOrWhiteSpace(model.ContractCode))
        {
            ModelState.AddModelError(nameof(model.ContractCode), "Vui lòng nhập mã hợp đồng.");
        }
        else if (await _context.RentalContracts.AnyAsync(item =>
            item.ContractCode == model.ContractCode
            && (!contractId.HasValue || item.ContractId != contractId.Value)))
        {
            ModelState.AddModelError(nameof(model.ContractCode), "Mã hợp đồng đã tồn tại.");
        }

        if (model.StartDate == default)
        {
            ModelState.AddModelError(nameof(model.StartDate), "Vui lòng nhập ngày bắt đầu.");
        }

        if (model.EndDate == default)
        {
            ModelState.AddModelError(nameof(model.EndDate), "Vui lòng nhập ngày kết thúc.");
        }

        if (model.StartDate != default
            && model.EndDate != default
            && startDate >= endDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "Ngày bắt đầu phải sớm hơn ngày kết thúc.");
        }

        if (model.MonthlyRent <= 0)
        {
            ModelState.AddModelError(nameof(model.MonthlyRent), "Giá thuê thỏa thuận phải lớn hơn 0.");
        }

        if (model.Deposit < 0)
        {
            ModelState.AddModelError(nameof(model.Deposit), "Tiền đặt cọc không được âm.");
        }

        var room = await _context.Rooms
            .AsNoTracking()
            .Where(item => item.RoomId == model.RoomId)
            .Select(item => new { item.RoomId, item.MaxOccupants, item.Status })
            .FirstOrDefaultAsync();

        if (room is null)
        {
            ModelState.AddModelError(nameof(model.RoomId), "Vui lòng chọn một phòng tồn tại.");
        }
        else if (room.Status == "Maintenance")
        {
            ModelState.AddModelError(nameof(model.RoomId), "Không thể lập hợp đồng cho phòng đang bảo trì.");
        }

        var selectedTenantIds = (model.TenantOptions ?? new List<ContractTenantOptionInput>())
            .Where(option => option.IsSelected)
            .Select(option => option.TenantId)
            .ToList();
        var distinctTenantIds = selectedTenantIds.Distinct().ToList();

        if (distinctTenantIds.Count == 0)
        {
            ModelState.AddModelError(nameof(model.TenantOptions), "Hãy thêm ít nhất một người thuê vào hợp đồng.");
        }

        if (selectedTenantIds.Count != distinctTenantIds.Count)
        {
            ModelState.AddModelError(nameof(model.TenantOptions), "Danh sách người thuê bị trùng. Vui lòng tải lại trang và chọn lại.");
        }

        if (distinctTenantIds.Count > 0)
        {
            var existingTenantIds = await _context.TenantProfiles
                .AsNoTracking()
                .Where(tenant => distinctTenantIds.Contains(tenant.TenantId))
                .Select(tenant => tenant.TenantId)
                .ToListAsync();

            if (existingTenantIds.Count != distinctTenantIds.Count)
            {
                ModelState.AddModelError(nameof(model.TenantOptions), "Một hoặc nhiều hồ sơ người thuê không còn tồn tại. Vui lòng tải lại trang.");
            }
        }

        if (!model.PrimaryTenantId.HasValue || !distinctTenantIds.Contains(model.PrimaryTenantId.Value))
        {
            ModelState.AddModelError(nameof(model.PrimaryTenantId), "Hãy chọn một người đại diện trong số những người đã thêm vào hợp đồng.");
        }

        if (room is not null && distinctTenantIds.Count > room.MaxOccupants)
        {
            ModelState.AddModelError(nameof(model.TenantOptions), $"Phòng này chỉ cho phép tối đa {room.MaxOccupants} người.");
        }

        if (contractStatus == "Active")
        {
            if (model.StartDate == default
                || model.EndDate == default
                || today < startDate
                || today > endDate)
            {
                ModelState.AddModelError(string.Empty, "Hợp đồng đang hiệu lực phải có ngày hiện tại nằm trong thời hạn hợp đồng.");
            }

            if (room is not null
                && model.StartDate != default
                && model.EndDate != default
                && startDate < endDate
                && await HasOverlappingActiveContractAsync(
                    model.RoomId,
                    model.StartDate,
                    model.EndDate,
                    contractId))
            {
                ModelState.AddModelError(nameof(model.RoomId), "Phòng đã có hợp đồng đang hiệu lực bị chồng lấn thời gian.");
            }
        }
    }

    private async Task<bool> HasOverlappingActiveContractAsync(
        int roomId,
        DateTime startDate,
        DateTime endDate,
        int? contractId)
    {
        var start = startDate.Date;
        var end = endDate.Date;

        return await _context.RentalContracts.AnyAsync(item =>
            item.RoomId == roomId
            && item.Status == "Active"
            && item.StartDate.Date <= end
            && item.EndDate.Date >= start
            && (!contractId.HasValue || item.ContractId != contractId.Value));
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
            await SynchronizeRoomStatusAsync(roomId);
        }

        await _context.SaveChangesAsync();
    }

    private async Task SynchronizeRoomStatusAsync(int roomId)
    {
        var room = await _context.Rooms.FindAsync(roomId);

        if (room is null || room.Status == "Maintenance")
        {
            return;
        }

        var today = DateTime.Today;
        var hasCurrentActiveContract = await _context.RentalContracts.AnyAsync(contract =>
            contract.RoomId == roomId
            && contract.Status == "Active"
            && contract.StartDate.Date <= today
            && contract.EndDate.Date >= today);

        room.Status = hasCurrentActiveContract ? "Occupied" : "Available";
    }

    private async Task PopulateFormOptionsAsync(
        RentalContractFormViewModel model,
        bool preserveTenantOptions)
    {
        var rooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        ViewBag.Rooms = rooms
            .Select(room => new RoomOptionViewModel
            {
                RoomId = room.RoomId,
                DisplayName = $"{room.Building.BuildingName} - Phòng {room.RoomCode}",
                MonthlyRent = room.MonthlyRent
            })
            .ToList();

        var submittedOptions = preserveTenantOptions
            ? (model.TenantOptions ?? new List<ContractTenantOptionInput>())
                .Where(option => option.TenantId > 0)
                .GroupBy(option => option.TenantId)
                .ToDictionary(group => group.Key, group => group.First())
            : new Dictionary<int, ContractTenantOptionInput>();

        var tenants = await _context.TenantProfiles
            .AsNoTracking()
            .OrderBy(tenant => tenant.FullName)
            .ToListAsync();

        model.TenantOptions = tenants.Select(tenant =>
        {
            submittedOptions.TryGetValue(tenant.TenantId, out var submitted);
            return new ContractTenantOptionInput
            {
                TenantId = tenant.TenantId,
                FullName = tenant.FullName,
                Phone = tenant.Phone ?? string.Empty,
                IsSelected = submitted?.IsSelected ?? false,
                RelationshipToPrimaryTenant = submitted?.RelationshipToPrimaryTenant
            };
        }).ToList();
    }

    private static RentalContract MapContract(RentalContractFormViewModel model)
    {
        return new RentalContract
        {
            ContractCode = model.ContractCode,
            RoomId = model.RoomId,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            MonthlyRent = model.MonthlyRent,
            Deposit = model.Deposit,
            Status = "Draft",
            Terms = model.Terms
        };
    }

    private static RentalContractFormViewModel MapForm(RentalContract contract)
    {
        return new RentalContractFormViewModel
        {
            ContractId = contract.ContractId,
            RoomId = contract.RoomId,
            ContractCode = contract.ContractCode,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            MonthlyRent = contract.MonthlyRent,
            Deposit = contract.Deposit,
            Terms = contract.Terms,
            PrimaryTenantId = contract.ContractTenants
                .Where(assignment => assignment.IsPrimaryTenant)
                .Select(assignment => (int?)assignment.TenantId)
                .FirstOrDefault(),
            TenantOptions = contract.ContractTenants.Select(assignment => new ContractTenantOptionInput
            {
                TenantId = assignment.TenantId,
                IsSelected = true,
                RelationshipToPrimaryTenant = assignment.RelationshipToPrimaryTenant
            }).ToList()
        };
    }

    private static void AddSelectedTenants(
        RentalContract contract,
        RentalContractFormViewModel model)
    {
        foreach (var option in model.TenantOptions.Where(option => option.IsSelected))
        {
            var isPrimary = option.TenantId == model.PrimaryTenantId;
            contract.ContractTenants.Add(new ContractTenant
            {
                TenantId = option.TenantId,
                IsPrimaryTenant = isPrimary,
                RelationshipToPrimaryTenant = isPrimary
                    ? null
                    : NormalizeOptionalText(option.RelationshipToPrimaryTenant),
                Contract = contract
            });
        }
    }

    private void SynchronizeTenantAssignments(
        RentalContract contract,
        RentalContractFormViewModel model)
    {
        var selectedOptions = model.TenantOptions
            .Where(option => option.IsSelected)
            .ToDictionary(option => option.TenantId);
        var existingAssignments = contract.ContractTenants.ToDictionary(item => item.TenantId);

        var removedAssignments = contract.ContractTenants
            .Where(item => !selectedOptions.ContainsKey(item.TenantId))
            .ToList();

        _context.ContractTenants.RemoveRange(removedAssignments);

        foreach (var option in selectedOptions.Values)
        {
            var isPrimary = option.TenantId == model.PrimaryTenantId;
            if (existingAssignments.TryGetValue(option.TenantId, out var existingAssignment))
            {
                existingAssignment.IsPrimaryTenant = isPrimary;
                existingAssignment.RelationshipToPrimaryTenant = isPrimary
                    ? null
                    : NormalizeOptionalText(option.RelationshipToPrimaryTenant);
            }
            else
            {
                _context.ContractTenants.Add(new ContractTenant
                {
                    ContractId = contract.ContractId,
                    TenantId = option.TenantId,
                    IsPrimaryTenant = isPrimary,
                    RelationshipToPrimaryTenant = isPrimary
                        ? null
                        : NormalizeOptionalText(option.RelationshipToPrimaryTenant)
                });
            }
        }
    }

    private static void NormalizeContract(RentalContractFormViewModel model)
    {
        model.ContractCode = model.ContractCode?.Trim() ?? string.Empty;
        model.Terms = NormalizeOptionalText(model.Terms);

        foreach (var option in model.TenantOptions ?? new List<ContractTenantOptionInput>())
        {
            option.RelationshipToPrimaryTenant = NormalizeOptionalText(option.RelationshipToPrimaryTenant);
        }
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalizedValue = value?.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }

    private async Task<bool> ContractExistsAsync(int id)
    {
        return await _context.RentalContracts.AnyAsync(contract => contract.ContractId == id);
    }
}
