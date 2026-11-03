using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;
using QuanLyNhaTro.ViewModels;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class MoveOutController : Controller
{
    private readonly ApplicationDbContext _context;

    public MoveOutController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Create(int contractId)
    {
        var contract = await LoadContractAsync(contractId);
        if (contract is null) return NotFound();
        if (contract.Status != "Active")
        {
            TempData["ErrorMessage"] = "Chỉ có thể lập phiếu quyết toán cho hợp đồng đang hiệu lực.";
            return RedirectToAction("Details", "RentalContract", new { id = contractId });
        }

        var existing = await _context.MoveOutSettlements.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ContractId == contractId);
        if (existing?.Status == "Confirmed")
            return RedirectToAction(nameof(Details), new { id = existing.SettlementId });

        return View(existing is null
            ? await BuildNewViewModelAsync(contract)
            : MapViewModel(existing));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MoveOutSettlementViewModel model)
    {
        var contract = await LoadContractAsync(model.ContractId);
        if (contract is null) return NotFound();
        if (contract.Status != "Active")
        {
            TempData["ErrorMessage"] = "Hợp đồng không còn ở trạng thái đang hiệu lực.";
            return RedirectToAction("Details", "RentalContract", new { id = model.ContractId });
        }

        var existing = await _context.MoveOutSettlements
            .FirstOrDefaultAsync(item => item.ContractId == model.ContractId);
        if (existing?.Status == "Confirmed")
            return RedirectToAction(nameof(Details), new { id = existing.SettlementId });

        model.ContractCode = contract.ContractCode;
        model.RoomLabel = $"{contract.Room.Building.BuildingName} - Phòng {contract.Room.RoomCode}";
        model.TenantNames = GetActiveTenantNames(contract);
        model.DepositAmount = contract.Deposit;
        model.OutstandingInvoiceAmount = await CalculateOutstandingInvoiceDebtAsync(contract.ContractId);

        var config = await _context.SystemConfigs.AsNoTracking()
            .OrderBy(item => item.ConfigId).FirstOrDefaultAsync();
        var latestInvoice = await _context.Invoices.AsNoTracking()
            .Where(item => item.ContractId == contract.ContractId)
            .OrderByDescending(item => item.BillingMonth).FirstOrDefaultAsync();
        model.ElectricityUnitPrice = config?.ElectricityPrice ?? latestInvoice?.ElectricityUnitPrice ?? 0m;
        model.WaterUnitPrice = config?.WaterPrice ?? latestInvoice?.WaterUnitPrice ?? 0m;
        model.HasPreviousReading = await _context.UtilityReadings.AnyAsync(item => item.RoomId == contract.RoomId);

        if (model.MoveOutDate == default || model.MoveOutDate.Date < contract.StartDate.Date
            || model.MoveOutDate.Date > DateTime.Today)
            ModelState.AddModelError(nameof(model.MoveOutDate), "Ngày trả phòng phải nằm từ ngày bắt đầu hợp đồng đến hôm nay.");
        if (model.FinalElectricityReading < model.PreviousElectricityReading)
            ModelState.AddModelError(nameof(model.FinalElectricityReading), "Chỉ số điện khi trả phòng không được nhỏ hơn chỉ số trước đó.");
        if (model.FinalWaterReading < model.PreviousWaterReading)
            ModelState.AddModelError(nameof(model.FinalWaterReading), "Chỉ số nước khi trả phòng không được nhỏ hơn chỉ số trước đó.");
        if (model.ElectricityUnitPrice <= 0m || model.WaterUnitPrice <= 0m)
            ModelState.AddModelError(string.Empty, "Chưa có đơn giá điện hoặc nước để tính quyết toán. Vui lòng cấu hình đơn giá trước.");

        if (!ModelState.IsValid) return View(model);

        var settlement = existing ?? new MoveOutSettlement
        {
            ContractId = contract.ContractId,
            CreatedAt = DateTime.Now
        };
        settlement.ContractCodeSnapshot = model.ContractCode;
        settlement.RoomLabelSnapshot = model.RoomLabel;
        settlement.TenantNamesSnapshot = model.TenantNames;
        settlement.MoveOutDate = model.MoveOutDate.Date;
        settlement.DepositAmount = model.DepositAmount;
        settlement.OutstandingInvoiceAmount = model.OutstandingInvoiceAmount;
        settlement.PreviousElectricityReading = model.PreviousElectricityReading;
        settlement.FinalElectricityReading = model.FinalElectricityReading;
        settlement.ElectricityUnitPrice = model.ElectricityUnitPrice;
        settlement.ElectricityAmount = model.ElectricityAmount;
        settlement.PreviousWaterReading = model.PreviousWaterReading;
        settlement.FinalWaterReading = model.FinalWaterReading;
        settlement.WaterUnitPrice = model.WaterUnitPrice;
        settlement.WaterAmount = model.WaterAmount;
        settlement.GarbageFee = model.GarbageFee;
        settlement.CleaningFee = model.CleaningFee;
        settlement.RepairFee = model.RepairFee;
        settlement.OtherFee = model.OtherFee;
        settlement.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
        settlement.Status = "Draft";
        if (existing is null) _context.MoveOutSettlements.Add(settlement);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Không thể lưu phiếu quyết toán. Vui lòng tải lại trang và thử lại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã lưu phiếu quyết toán. Hãy kiểm tra và in phiếu trước khi xác nhận trả phòng.";
        return RedirectToAction(nameof(Details), new { id = settlement.SettlementId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var settlement = await LoadSettlementAsync(id);
        return settlement is null ? NotFound() : View(MapViewModel(settlement));
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var settlement = await LoadSettlementAsync(id);
        return settlement is null ? NotFound() : View(MapViewModel(settlement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(int id)
    {
        var settlement = await _context.MoveOutSettlements
            .Include(item => item.Contract).ThenInclude(contract => contract.Room)
            .Include(item => item.Contract).ThenInclude(contract => contract.ContractTenants)
            .FirstOrDefaultAsync(item => item.SettlementId == id);
        if (settlement is null) return NotFound();

        if (settlement.Status != "Draft" || settlement.Contract.Status != "Active")
        {
            TempData["ErrorMessage"] = "Phiếu quyết toán không còn ở trạng thái có thể xác nhận.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var moveOutDate = settlement.MoveOutDate.Date;
            settlement.Contract.Status = "Terminated";
            settlement.Contract.ActualEndDate = moveOutDate;
            settlement.Contract.ExpectedMoveOutDate = null;

            foreach (var occupant in settlement.Contract.ContractTenants.Where(item => item.IsActive))
            {
                occupant.IsActive = false;
                occupant.LeftAt = moveOutDate;
            }

            if (settlement.Contract.Room.Status != "Maintenance")
                settlement.Contract.Room.Status = "Cleaning";

            settlement.Status = "Confirmed";
            settlement.ConfirmedAt = DateTime.Now;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = "Không thể xác nhận trả phòng. Vui lòng thử lại.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TempData["SuccessMessage"] = "Đã xác nhận trả phòng và chấm dứt hợp đồng. Việc hoàn tiền cọc hoặc thu thêm được theo dõi riêng.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<RentalContract?> LoadContractAsync(int id) =>
        await _context.RentalContracts
            .Include(item => item.Room).ThenInclude(room => room.Building)
            .Include(item => item.ContractTenants).ThenInclude(assignment => assignment.Tenant)
            .FirstOrDefaultAsync(item => item.ContractId == id);

    private async Task<MoveOutSettlement?> LoadSettlementAsync(int id) =>
        await _context.MoveOutSettlements
            .Include(item => item.Contract).ThenInclude(contract => contract.Room).ThenInclude(room => room.Building)
            .Include(item => item.Contract).ThenInclude(contract => contract.ContractTenants).ThenInclude(assignment => assignment.Tenant)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.SettlementId == id);

    private async Task<MoveOutSettlementViewModel> BuildNewViewModelAsync(RentalContract contract)
    {
        var reading = await _context.UtilityReadings.AsNoTracking()
            .Where(item => item.RoomId == contract.RoomId)
            .OrderByDescending(item => item.ReadingMonth)
            .ThenByDescending(item => item.RecordedAt)
            .FirstOrDefaultAsync();
        var config = await _context.SystemConfigs.AsNoTracking()
            .OrderBy(item => item.ConfigId).FirstOrDefaultAsync();
        var latestInvoice = await _context.Invoices.AsNoTracking()
            .Where(item => item.ContractId == contract.ContractId)
            .OrderByDescending(item => item.BillingMonth).FirstOrDefaultAsync();

        return new MoveOutSettlementViewModel
        {
            ContractId = contract.ContractId,
            ContractCode = contract.ContractCode,
            RoomLabel = $"{contract.Room.Building.BuildingName} - Phòng {contract.Room.RoomCode}",
            TenantNames = GetActiveTenantNames(contract),
            MoveOutDate = DateTime.Today,
            DepositAmount = contract.Deposit,
            OutstandingInvoiceAmount = await CalculateOutstandingInvoiceDebtAsync(contract.ContractId),
            PreviousElectricityReading = reading?.ElectricityReading ?? 0m,
            FinalElectricityReading = reading?.ElectricityReading ?? 0m,
            PreviousWaterReading = reading?.WaterReading ?? 0m,
            FinalWaterReading = reading?.WaterReading ?? 0m,
            ElectricityUnitPrice = config?.ElectricityPrice ?? latestInvoice?.ElectricityUnitPrice ?? 0m,
            WaterUnitPrice = config?.WaterPrice ?? latestInvoice?.WaterUnitPrice ?? 0m,
            GarbageFee = config?.GarbageFee ?? 0m,
            HasPreviousReading = reading is not null
        };
    }

    private async Task<decimal> CalculateOutstandingInvoiceDebtAsync(int contractId)
    {
        var invoices = await _context.Invoices.Include(item => item.Receipts)
            .AsNoTracking().Where(item => item.ContractId == contractId).ToListAsync();
        decimal debt = 0m;
        foreach (var invoice in invoices)
        {
            var paid = invoice.Receipts.Sum(receipt => receipt.Amount);
            if (paid == 0m && invoice.Status == "Paid") paid = invoice.TotalAmount;
            paid = Math.Min(paid, invoice.TotalAmount);
            debt += Math.Max(invoice.TotalAmount - paid, 0m);
        }
        return debt;
    }

    private static string GetActiveTenantNames(RentalContract contract) =>
        string.Join(", ", contract.ContractTenants.Where(item => item.IsActive)
            .OrderByDescending(item => item.IsPrimaryTenant)
            .ThenBy(item => item.Tenant.FullName)
            .Select(item => item.Tenant.FullName));

    private static MoveOutSettlementViewModel MapViewModel(MoveOutSettlement item) => new()
    {
        SettlementId = item.SettlementId,
        ContractId = item.ContractId,
        ContractCode = item.ContractCodeSnapshot,
        RoomLabel = item.RoomLabelSnapshot,
        TenantNames = item.TenantNamesSnapshot,
        MoveOutDate = item.MoveOutDate,
        DepositAmount = item.DepositAmount,
        OutstandingInvoiceAmount = item.OutstandingInvoiceAmount,
        PreviousElectricityReading = item.PreviousElectricityReading,
        FinalElectricityReading = item.FinalElectricityReading,
        ElectricityUnitPrice = item.ElectricityUnitPrice,
        PreviousWaterReading = item.PreviousWaterReading,
        FinalWaterReading = item.FinalWaterReading,
        WaterUnitPrice = item.WaterUnitPrice,
        GarbageFee = item.GarbageFee,
        CleaningFee = item.CleaningFee,
        RepairFee = item.RepairFee,
        OtherFee = item.OtherFee,
        Notes = item.Notes,
        Status = item.Status,
        HasPreviousReading = true
    };
}
