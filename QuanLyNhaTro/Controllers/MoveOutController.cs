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
        if (contract.Status is not ("Active" or "Expired"))
        {
            TempData["ErrorMessage"] = "Chỉ có thể lập phiếu quyết toán cho hợp đồng đang hiệu lực hoặc đã hết hạn.";
            return RedirectToAction("Details", "RentalContract", new { id = contractId });
        }

        var existing = await _context.MoveOutSettlements.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ContractId == contractId);
        if (existing?.Status == "Confirmed")
            return RedirectToAction(nameof(Details), new { id = existing.SettlementId });

        var model = existing is null
            ? await BuildNewViewModelAsync(contract)
            : MapViewModel(existing);

        var invoice = await FindMoveOutMonthInvoiceAsync(contractId, model.MoveOutDate);
        model.OutstandingInvoiceAmount = await CalculateOutstandingInvoiceDebtAsync(contractId);
        model.HasCurrentMonthInvoice = invoice is not null;
        model.IsFinalReadingLocked = existing is not null && invoice is not null;
        model.IsInvoiceReconciled = invoice is not null || existing?.IsInvoiceReconciled == true;
        model.GarbageFee = 0m;

        if (invoice is not null)
        {
            model.ElectricityUnitPrice = invoice.ElectricityUnitPrice;
            model.WaterUnitPrice = invoice.WaterUnitPrice;

            if (existing is not null)
            {
                model.InvoiceAdjustmentAmount = await CalculateInvoiceAdjustmentAmountAsync(
                    contract, model.MoveOutDate,
                    model.PreviousElectricityReading, model.FinalElectricityReading,
                    model.PreviousWaterReading, model.FinalWaterReading);
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MoveOutSettlementViewModel model)
    {
        var contract = await LoadContractAsync(model.ContractId);
        if (contract is null) return NotFound();
        if (contract.Status is not ("Active" or "Expired"))
        {
            TempData["ErrorMessage"] = "Chỉ có thể lập quyết toán cho hợp đồng đang hiệu lực hoặc đã hết hạn.";
            return RedirectToAction("Details", "RentalContract", new { id = model.ContractId });
        }

        var existing = await _context.MoveOutSettlements
            .FirstOrDefaultAsync(item => item.ContractId == model.ContractId);
        if (existing?.Status == "Confirmed")
            return RedirectToAction(nameof(Details), new { id = existing.SettlementId });

        var lookupDate = model.MoveOutDate == default ? DateTime.Today : model.MoveOutDate.Date;
        var invoiceForSavedDate = existing is null
            ? null
            : await FindMoveOutMonthInvoiceAsync(contract.ContractId, existing.MoveOutDate);
        var finalReadingLocked = existing is not null && invoiceForSavedDate is not null;

        if (finalReadingLocked)
        {
            if (lookupDate != existing!.MoveOutDate.Date
                || model.FinalElectricityReading != existing.FinalElectricityReading
                || model.FinalWaterReading != existing.FinalWaterReading)
            {
                ModelState.AddModelError(string.Empty,
                    "Hóa đơn tháng trả phòng đã được lập theo ngày và chỉ số chốt đã lưu. Không thể thay đổi các thông tin này sau khi lập hóa đơn.");
            }

            model.MoveOutDate = existing!.MoveOutDate;
            model.FinalElectricityReading = existing.FinalElectricityReading;
            model.FinalWaterReading = existing.FinalWaterReading;
            model.PreviousElectricityReading = existing.PreviousElectricityReading;
            model.PreviousWaterReading = existing.PreviousWaterReading;
            ModelState.Remove(nameof(model.MoveOutDate));
            ModelState.Remove(nameof(model.FinalElectricityReading));
            ModelState.Remove(nameof(model.FinalWaterReading));
            lookupDate = existing.MoveOutDate.Date;
        }
        else
        {
            var baseline = await GetMoveOutBaselineReadingsAsync(contract, lookupDate);
            model.PreviousElectricityReading = baseline.Electricity ?? 0m;
            model.PreviousWaterReading = baseline.Water ?? 0m;
            model.HasPreviousReading = baseline.Electricity.HasValue && baseline.Water.HasValue;

            if (!model.HasPreviousReading)
            {
                ModelState.AddModelError(string.Empty,
                    "Không tìm thấy đủ chỉ số điện nước đầu kỳ. Hãy bổ sung chỉ số bàn giao cho hợp đồng hoặc ghi nhận kỳ điện nước trước đó.");
            }
        }

        ModelState.Remove(nameof(model.PreviousElectricityReading));
        ModelState.Remove(nameof(model.PreviousWaterReading));

        model.ContractCode = contract.ContractCode;
        model.RoomLabel = $"{contract.Room.Building.BuildingName} - Phòng {contract.Room.RoomCode}";
        model.TenantNames = GetActiveTenantNames(contract);
        model.DepositAmount = contract.Deposit;
        model.OutstandingInvoiceAmount = await CalculateOutstandingInvoiceDebtAsync(contract.ContractId);
        model.IsFinalReadingLocked = finalReadingLocked;
        model.GarbageFee = 0m;
        model.HasCurrentMonthInvoice = await FindMoveOutMonthInvoiceAsync(contract.ContractId, lookupDate) is not null;
        model.IsInvoiceReconciled = model.HasCurrentMonthInvoice || existing?.IsInvoiceReconciled == true;

        var config = await _context.SystemConfigs.AsNoTracking()
            .OrderBy(item => item.ConfigId).FirstOrDefaultAsync();
        var periodConfig = await _context.InvoicePeriodConfigs.AsNoTracking()
            .FirstOrDefaultAsync(item => item.BillingMonth == new DateTime(lookupDate.Year, lookupDate.Month, 1));
        var latestInvoice = await _context.Invoices.AsNoTracking()
            .Where(item => item.ContractId == contract.ContractId)
            .OrderByDescending(item => item.BillingMonth).FirstOrDefaultAsync();
        var invoice = model.HasCurrentMonthInvoice
            ? await FindMoveOutMonthInvoiceAsync(contract.ContractId, lookupDate)
            : null;

        model.ElectricityUnitPrice = invoice?.ElectricityUnitPrice
            ?? periodConfig?.ElectricityPrice
            ?? config?.ElectricityPrice
            ?? latestInvoice?.ElectricityUnitPrice
            ?? 0m;
        model.WaterUnitPrice = invoice?.WaterUnitPrice
            ?? periodConfig?.WaterPrice
            ?? config?.WaterPrice
            ?? latestInvoice?.WaterUnitPrice
            ?? 0m;
        model.InvoiceAdjustmentAmount = invoice is null
            ? 0m
            : await CalculateInvoiceAdjustmentAmountAsync(
                contract, lookupDate,
                model.PreviousElectricityReading, model.FinalElectricityReading,
                model.PreviousWaterReading, model.FinalWaterReading);

        if (model.MoveOutDate == default
            || model.MoveOutDate.Date < contract.StartDate.Date
            || model.MoveOutDate.Date > contract.EndDate.Date
            || model.MoveOutDate.Date > DateTime.Today)
        {
            ModelState.AddModelError(nameof(model.MoveOutDate),
                "Ngày trả phòng phải nằm trong thời hạn hợp đồng và không được sau hôm nay.");
        }

        if (model.FinalElectricityReading < model.PreviousElectricityReading)
            ModelState.AddModelError(nameof(model.FinalElectricityReading),
                "Chỉ số điện khi trả phòng không được nhỏ hơn chỉ số trước đó.");
        if (model.FinalWaterReading < model.PreviousWaterReading)
            ModelState.AddModelError(nameof(model.FinalWaterReading),
                "Chỉ số nước khi trả phòng không được nhỏ hơn chỉ số trước đó.");
        if (model.ElectricityUnitPrice <= 0m || model.WaterUnitPrice <= 0m)
            ModelState.AddModelError(string.Empty,
                "Chưa có đơn giá điện hoặc nước để tính quyết toán. Vui lòng cấu hình đơn giá trước.");

        if (!ModelState.IsValid)
            return View(model);

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
        settlement.InvoiceAdjustmentAmount = model.InvoiceAdjustmentAmount;
        settlement.IsInvoiceReconciled = model.IsInvoiceReconciled;
        settlement.PreviousElectricityReading = model.PreviousElectricityReading;
        settlement.FinalElectricityReading = model.FinalElectricityReading;
        settlement.ElectricityUnitPrice = model.ElectricityUnitPrice;
        settlement.ElectricityAmount = model.ElectricityAmount;
        settlement.PreviousWaterReading = model.PreviousWaterReading;
        settlement.FinalWaterReading = model.FinalWaterReading;
        settlement.WaterUnitPrice = model.WaterUnitPrice;
        settlement.WaterAmount = model.WaterAmount;
        settlement.GarbageFee = 0m;
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
            ModelState.AddModelError(string.Empty,
                "Không thể lưu phiếu quyết toán. Vui lòng tải lại trang và thử lại.");
            return View(model);
        }

        TempData["SuccessMessage"] = model.HasCurrentMonthInvoice
            ? "Đã lưu phiếu quyết toán và đối soát với hóa đơn tháng trả phòng."
            : "Đã lưu phiếu quyết toán. Tiếp theo hãy lập hóa đơn tháng trả phòng trước khi xác nhận.";
        return RedirectToAction(nameof(Details), new { id = settlement.SettlementId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var settlement = await LoadSettlementAsync(id);
        if (settlement is null) return NotFound();

        var model = MapViewModel(settlement);
        var moveOutInvoice = await FindMoveOutMonthInvoiceAsync(
            settlement.ContractId, settlement.MoveOutDate);
        model.HasCurrentMonthInvoice = moveOutInvoice is not null;
        if (settlement.Status == "Draft")
        {
            model.IsInvoiceReconciled = model.HasCurrentMonthInvoice;
            model.OutstandingInvoiceAmount = await CalculateOutstandingInvoiceDebtAsync(settlement.ContractId);
            if (moveOutInvoice is not null)
            {
                model.ElectricityUnitPrice = moveOutInvoice.ElectricityUnitPrice;
                model.WaterUnitPrice = moveOutInvoice.WaterUnitPrice;
                model.InvoiceAdjustmentAmount = await CalculateInvoiceAdjustmentAmountAsync(
                    settlement.Contract, settlement.MoveOutDate,
                    settlement.PreviousElectricityReading, settlement.FinalElectricityReading,
                    settlement.PreviousWaterReading, settlement.FinalWaterReading);
            }
        }

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        var settlement = await LoadSettlementAsync(id);
        if (settlement is null) return NotFound();

        var model = MapViewModel(settlement);
        var moveOutInvoice = await FindMoveOutMonthInvoiceAsync(
            settlement.ContractId, settlement.MoveOutDate);
        model.HasCurrentMonthInvoice = moveOutInvoice is not null;
        if (settlement.Status == "Draft")
        {
            model.IsInvoiceReconciled = model.HasCurrentMonthInvoice;
            model.OutstandingInvoiceAmount = await CalculateOutstandingInvoiceDebtAsync(settlement.ContractId);
            if (moveOutInvoice is not null)
            {
                model.ElectricityUnitPrice = moveOutInvoice.ElectricityUnitPrice;
                model.WaterUnitPrice = moveOutInvoice.WaterUnitPrice;
                model.InvoiceAdjustmentAmount = await CalculateInvoiceAdjustmentAmountAsync(
                    settlement.Contract, settlement.MoveOutDate,
                    settlement.PreviousElectricityReading, settlement.FinalElectricityReading,
                    settlement.PreviousWaterReading, settlement.FinalWaterReading);
            }
        }

        return View(model);
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

        if (settlement.Status != "Draft"
            || settlement.Contract.Status is not ("Active" or "Expired"))
        {
            TempData["ErrorMessage"] = "Phiếu quyết toán không còn ở trạng thái có thể xác nhận.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var moveOutMonthInvoice = await FindMoveOutMonthInvoiceAsync(
            settlement.ContractId, settlement.MoveOutDate);
        if (moveOutMonthInvoice is null)
        {
            TempData["ErrorMessage"] = "Chưa có hóa đơn tháng trả phòng. Hãy lập hóa đơn cho tháng này trước khi xác nhận để tiền phòng, điện nước và phí định kỳ được đối soát đầy đủ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var currentDebt = await CalculateOutstandingInvoiceDebtAsync(settlement.ContractId);
            var currentAdjustment = await CalculateInvoiceAdjustmentAmountAsync(
                settlement.Contract, settlement.MoveOutDate,
                settlement.PreviousElectricityReading, settlement.FinalElectricityReading,
                settlement.PreviousWaterReading, settlement.FinalWaterReading);
            var expectedElectricityAmount = decimal.Round(
                Math.Max(settlement.FinalElectricityReading - settlement.PreviousElectricityReading, 0m)
                    * moveOutMonthInvoice.ElectricityUnitPrice,
                0, MidpointRounding.AwayFromZero);
            var expectedWaterAmount = decimal.Round(
                Math.Max(settlement.FinalWaterReading - settlement.PreviousWaterReading, 0m)
                    * moveOutMonthInvoice.WaterUnitPrice,
                0, MidpointRounding.AwayFromZero);
            var financialDataChanged = currentDebt != settlement.OutstandingInvoiceAmount
                || !settlement.IsInvoiceReconciled
                || settlement.DepositAmount != settlement.Contract.Deposit
                || currentAdjustment != settlement.InvoiceAdjustmentAmount
                || settlement.ElectricityUnitPrice != moveOutMonthInvoice.ElectricityUnitPrice
                || settlement.WaterUnitPrice != moveOutMonthInvoice.WaterUnitPrice
                || settlement.ElectricityAmount != expectedElectricityAmount
                || settlement.WaterAmount != expectedWaterAmount
                || settlement.GarbageFee != 0m;

            if (financialDataChanged)
            {
                settlement.OutstandingInvoiceAmount = currentDebt;
                settlement.DepositAmount = settlement.Contract.Deposit;
                settlement.InvoiceAdjustmentAmount = currentAdjustment;
                settlement.IsInvoiceReconciled = true;
                settlement.ElectricityUnitPrice = moveOutMonthInvoice.ElectricityUnitPrice;
                settlement.WaterUnitPrice = moveOutMonthInvoice.WaterUnitPrice;
                settlement.ElectricityAmount = expectedElectricityAmount;
                settlement.WaterAmount = expectedWaterAmount;
                settlement.GarbageFee = 0m;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                TempData["ErrorMessage"] = "Công nợ, khoản điều chỉnh hóa đơn hoặc tiền cọc đã thay đổi. Phiếu được cập nhật; vui lòng kiểm tra lại trước khi xác nhận.";
                return RedirectToAction(nameof(Details), new { id });
            }

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
            {
                var hasAnotherActiveContract = await _context.RentalContracts.AnyAsync(item =>
                    item.ContractId != settlement.ContractId
                    && item.RoomId == settlement.Contract.RoomId
                    && item.Status == "Active"
                    && item.StartDate.Date <= DateTime.Today
                    && item.EndDate.Date >= DateTime.Today);

                settlement.Contract.Room.Status = hasAnotherActiveContract ? "Occupied" : "Cleaning";
            }

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
        var moveOutDate = contract.Status == "Expired" && contract.EndDate.Date < DateTime.Today
            ? contract.EndDate.Date
            : DateTime.Today;
        var baseline = await GetMoveOutBaselineReadingsAsync(contract, moveOutDate);
        var config = await _context.SystemConfigs.AsNoTracking()
            .OrderBy(item => item.ConfigId).FirstOrDefaultAsync();
        var latestInvoice = await _context.Invoices.AsNoTracking()
            .Where(item => item.ContractId == contract.ContractId)
            .OrderByDescending(item => item.BillingMonth).FirstOrDefaultAsync();
        var moveOutInvoice = await FindMoveOutMonthInvoiceAsync(contract.ContractId, moveOutDate);

        return new MoveOutSettlementViewModel
        {
            ContractId = contract.ContractId,
            ContractCode = contract.ContractCode,
            RoomLabel = $"{contract.Room.Building.BuildingName} - Phòng {contract.Room.RoomCode}",
            TenantNames = GetActiveTenantNames(contract),
            MoveOutDate = moveOutDate,
            DepositAmount = contract.Deposit,
            OutstandingInvoiceAmount = await CalculateOutstandingInvoiceDebtAsync(contract.ContractId),
            PreviousElectricityReading = baseline.Electricity ?? 0m,
            FinalElectricityReading = baseline.Electricity ?? 0m,
            PreviousWaterReading = baseline.Water ?? 0m,
            FinalWaterReading = baseline.Water ?? 0m,
            ElectricityUnitPrice = moveOutInvoice?.ElectricityUnitPrice ?? config?.ElectricityPrice ?? latestInvoice?.ElectricityUnitPrice ?? 0m,
            WaterUnitPrice = moveOutInvoice?.WaterUnitPrice ?? config?.WaterPrice ?? latestInvoice?.WaterUnitPrice ?? 0m,
            GarbageFee = 0m,
            HasPreviousReading = baseline.Electricity.HasValue && baseline.Water.HasValue,
            HasCurrentMonthInvoice = moveOutInvoice is not null
        };
    }

    private async Task<Invoice?> FindMoveOutMonthInvoiceAsync(int contractId, DateTime moveOutDate)
    {
        var billingMonth = new DateTime(moveOutDate.Year, moveOutDate.Month, 1);
        return await _context.Invoices.AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.ContractId == contractId && item.BillingMonth == billingMonth);
    }

    private async Task<(decimal? Electricity, decimal? Water)> GetMoveOutBaselineReadingsAsync(
        RentalContract contract,
        DateTime moveOutDate)
    {
        var billingMonth = new DateTime(moveOutDate.Year, moveOutDate.Month, 1);
        if (contract.StartDate.Date >= billingMonth)
        {
            return (contract.MoveInElectricityReading, contract.MoveInWaterReading);
        }

        var previousReading = await _context.UtilityReadings.AsNoTracking()
            .Where(item => item.RoomId == contract.RoomId && item.ReadingMonth < billingMonth)
            .OrderByDescending(item => item.ReadingMonth)
            .ThenByDescending(item => item.RecordedAt)
            .FirstOrDefaultAsync();

        return (previousReading?.ElectricityReading, previousReading?.WaterReading);
    }

    private async Task<decimal> CalculateInvoiceAdjustmentAmountAsync(
        RentalContract contract,
        DateTime moveOutDate,
        decimal previousElectricityReading,
        decimal finalElectricityReading,
        decimal previousWaterReading,
        decimal finalWaterReading)
    {
        var invoice = await FindMoveOutMonthInvoiceAsync(contract.ContractId, moveOutDate);
        if (invoice is null)
        {
            return 0m;
        }

        var billingMonth = new DateTime(moveOutDate.Year, moveOutDate.Month, 1);
        var monthEnd = billingMonth.AddMonths(1).AddDays(-1);
        var invoiceStart = invoice.BillingStartDate?.Date ?? billingMonth;
        var invoiceEnd = invoice.BillingEndDate?.Date ?? monthEnd;
        var invoiceDays = Math.Max((invoiceEnd - invoiceStart).Days + 1, 1);

        var expectedStart = contract.StartDate.Date > billingMonth
            ? contract.StartDate.Date
            : billingMonth;
        var expectedDays = Math.Max((moveOutDate.Date - expectedStart).Days + 1, 0);
        if (expectedDays == 0)
        {
            return -invoice.TotalAmount;
        }

        var rent = ProrateAmount(invoice.RentAmount, expectedDays, invoiceDays);
        var electricity = decimal.Round(
            Math.Max(finalElectricityReading - previousElectricityReading, 0m) * invoice.ElectricityUnitPrice,
            0, MidpointRounding.AwayFromZero);
        var water = decimal.Round(
            Math.Max(finalWaterReading - previousWaterReading, 0m) * invoice.WaterUnitPrice,
            0, MidpointRounding.AwayFromZero);
        var garbage = ProrateAmount(invoice.GarbageFee, expectedDays, invoiceDays);
        var wifi = ProrateAmount(invoice.WifiFee, expectedDays, invoiceDays);
        var discount = ProrateAmount(invoice.DiscountAmount, expectedDays, invoiceDays);
        var expectedTotal = rent + electricity + water + garbage + wifi - discount;

        return expectedTotal - invoice.TotalAmount;
    }

    private static decimal ProrateAmount(decimal amount, int occupiedDays, int sourceDays)
    {
        if (amount <= 0m || occupiedDays <= 0 || sourceDays <= 0)
        {
            return 0m;
        }

        return decimal.Round(
            amount * occupiedDays / sourceDays,
            0,
            MidpointRounding.AwayFromZero);
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
        InvoiceAdjustmentAmount = item.InvoiceAdjustmentAmount,
        IsInvoiceReconciled = item.IsInvoiceReconciled,
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
