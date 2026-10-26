using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class InvoiceController : Controller
{
    private readonly ApplicationDbContext _context;

    public InvoiceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? billingMonth)
    {
        if (!TryParseBillingMonth(billingMonth, out var period))
        {
            period = CurrentMonth();
        }

        var currentMonth = CurrentMonth();
        if (period > currentMonth)
        {
            period = currentMonth;
            TempData["ErrorMessage"] = "Chưa thể lập hóa đơn cho kỳ trong tương lai.";
        }

        var invoices = await _context.Invoices
            .Where(invoice => invoice.BillingMonth == period)
            .Include(invoice => invoice.Contract)
                .ThenInclude(contract => contract.Room)
                    .ThenInclude(room => room.Building)
            .Include(invoice => invoice.Contract)
                .ThenInclude(contract => contract.ContractTenants)
                    .ThenInclude(assignment => assignment.Tenant)
            .AsNoTracking()
            .OrderBy(invoice => invoice.RoomLabelSnapshot)
            .ThenBy(invoice => invoice.InvoiceCode)
            .ToListAsync();

        ViewBag.BillingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        ViewBag.CurrentMonth = currentMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        ViewBag.BillingMonthLabel = $"T{period.Month} {period.Year}";
        ViewBag.SystemDate = DateTime.Today.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        ViewBag.PaidInvoiceCount = invoices.Count(invoice => invoice.Status == "Paid");
        ViewBag.UnpaidInvoiceCount = invoices.Count(invoice => invoice.Status != "Paid");
        ViewBag.CollectedAmount = invoices
            .Where(invoice => invoice.Status == "Paid")
            .Sum(invoice => invoice.TotalAmount);
        ViewBag.OutstandingAmount = invoices
            .Where(invoice => invoice.Status != "Paid")
            .Sum(invoice => invoice.TotalAmount);
        ViewBag.ElectricityAmount = invoices.Sum(invoice => invoice.ElectricityAmount);
        ViewBag.WaterAmount = invoices.Sum(invoice => invoice.WaterAmount);
        ViewBag.TotalAmount = invoices.Sum(invoice => invoice.TotalAmount);
        return View(invoices);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePaymentStatus(int id, bool isPaid)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(item => item.InvoiceId == id);

        if (invoice is null)
        {
            return NotFound(new { success = false, message = "Không tìm thấy hóa đơn." });
        }

        invoice.Status = isPaid ? "Paid" : "Unpaid";

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                success = false,
                message = "Không thể cập nhật trạng thái thu tiền. Vui lòng thử lại."
            });
        }

        return Json(new
        {
            success = true,
            isPaid,
            message = isPaid ? "Đã ghi nhận hóa đơn đã thu." : "Đã chuyển hóa đơn về chưa thu."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAmounts(
        int id,
        decimal rentAmount,
        decimal garbageFee,
        decimal previousDebtAmount,
        decimal discountAmount,
        string? billingMonth)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(item => item.InvoiceId == id);

        if (invoice is null)
        {
            return NotFound();
        }

        var returnPeriod = TryParseBillingMonth(billingMonth, out var parsedPeriod)
            ? parsedPeriod
            : new DateTime(invoice.BillingMonth.Year, invoice.BillingMonth.Month, 1);
        var returnMonth = returnPeriod.ToString("yyyy-MM", CultureInfo.InvariantCulture);

        if (invoice.Status == "Paid")
        {
            TempData["ErrorMessage"] = "Không thể sửa khoản tiền của hóa đơn đã ghi nhận đã thu.";
            return RedirectToAction(nameof(Index), new { billingMonth = returnMonth });
        }

        if (rentAmount < 0m || garbageFee < 0m || previousDebtAmount < 0m || discountAmount < 0m)
        {
            TempData["ErrorMessage"] = "Các khoản tiền không được nhỏ hơn 0.";
            return RedirectToAction(nameof(Index), new { billingMonth = returnMonth });
        }

        var amountBeforeDiscount = rentAmount
            + invoice.ElectricityAmount
            + invoice.WaterAmount
            + garbageFee
            + invoice.WifiFee
            + previousDebtAmount;

        if (discountAmount > amountBeforeDiscount)
        {
            TempData["ErrorMessage"] = "Tiền giảm trừ không được lớn hơn tổng tiền trước khi giảm.";
            return RedirectToAction(nameof(Index), new { billingMonth = returnMonth });
        }

        invoice.RentAmount = rentAmount;
        invoice.GarbageFee = garbageFee;
        invoice.PreviousDebtAmount = previousDebtAmount;
        invoice.DiscountAmount = discountAmount;
        invoice.TotalAmount = amountBeforeDiscount - discountAmount;

        try
        {
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã cập nhật hóa đơn {invoice.InvoiceCode}.";
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Không thể lưu các khoản tiền. Vui lòng thử lại.";
        }

        return RedirectToAction(nameof(Index), new { billingMonth = returnMonth });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> GenerateMonth(string? billingMonth)
    {
        if (!TryParseBillingMonth(billingMonth, out var period))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn kỳ hóa đơn hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var currentMonth = CurrentMonth();
        if (period > currentMonth)
        {
            TempData["ErrorMessage"] = "Chưa thể lập hóa đơn cho kỳ trong tương lai.";
            return RedirectToAction(nameof(Index), new
            {
                billingMonth = currentMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        var periodEnd = period.AddMonths(1).AddDays(-1);
        var config = await _context.SystemConfigs
            .AsNoTracking()
            .OrderBy(item => item.ConfigId)
            .FirstOrDefaultAsync();

        if (config is null)
        {
            TempData["ErrorMessage"] = "Chưa có cấu hình đơn giá. Hãy hoàn tất mục Cấu hình trước khi lập hóa đơn.";
            return RedirectToAction(nameof(Index), new
            {
                billingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        // Only bill contracts that cover the entire month. Partial-month move-in/move-out
        // settlement is intentionally kept for the later contract-termination workflow.
        var contracts = await _context.RentalContracts
            .Where(contract =>
                contract.Status != "Draft"
                && contract.Status != "Cancelled"
                && contract.StartDate.Date <= period
                && (
                    (contract.ActualEndDate == null && contract.EndDate.Date >= periodEnd)
                    || (contract.ActualEndDate != null && contract.ActualEndDate.Value.Date >= periodEnd)
                ))
            .Include(contract => contract.Room)
                .ThenInclude(room => room.Building)
            .Include(contract => contract.ContractTenants)
                .ThenInclude(assignment => assignment.Tenant)
            .AsNoTracking()
            .OrderBy(contract => contract.Room.Building.BuildingName)
            .ThenBy(contract => contract.Room.RoomCode)
            .ToListAsync();

        if (contracts.Count == 0)
        {
            TempData["ErrorMessage"] = "Không tìm thấy hợp đồng bao phủ trọn tháng này. Hợp đồng bản nháp và kỳ thuê không trọn tháng chưa được lập tự động.";
            return RedirectToAction(nameof(Index), new
            {
                billingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        var existingContractIds = await _context.Invoices
            .Where(invoice => invoice.BillingMonth == period)
            .Select(invoice => invoice.ContractId)
            .ToListAsync();
        var existingSet = existingContractIds.ToHashSet();

        var conflictingRoomIds = contracts
            .GroupBy(contract => contract.RoomId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet();

        var roomIds = contracts.Select(contract => contract.RoomId).Distinct().ToList();
        var previousMonth = period.AddMonths(-1);
        var readings = await _context.UtilityReadings
            .AsNoTracking()
            .Where(reading =>
                roomIds.Contains(reading.RoomId)
                && (reading.ReadingMonth == previousMonth || reading.ReadingMonth == period))
            .ToListAsync();
        var readingsByRoomAndMonth = readings.ToDictionary(
            reading => (reading.RoomId, reading.ReadingMonth));

        var newInvoices = new List<Invoice>();
        var errors = new List<string>();
        var alreadyCreatedCount = 0;

        foreach (var contract in contracts)
        {
            var roomLabel = $"{contract.Room.Building.BuildingName} - Phòng {contract.Room.RoomCode}";

            if (conflictingRoomIds.Contains(contract.RoomId))
            {
                errors.Add($"{roomLabel}: có nhiều hợp đồng cùng bao phủ kỳ này; hãy kiểm tra thời hạn hợp đồng.");
                continue;
            }

            if (existingSet.Contains(contract.ContractId))
            {
                alreadyCreatedCount++;
                continue;
            }

            if (!readingsByRoomAndMonth.TryGetValue((contract.RoomId, previousMonth), out var previousReading)
                || !readingsByRoomAndMonth.TryGetValue((contract.RoomId, period), out var currentReading))
            {
                errors.Add($"{roomLabel}: cần có chỉ số điện nước của cả {previousMonth:MM/yyyy} và {period:MM/yyyy}.");
                continue;
            }

            var electricityConsumption = currentReading.ElectricityReading - previousReading.ElectricityReading;
            var waterConsumption = currentReading.WaterReading - previousReading.WaterReading;

            if (electricityConsumption < 0 || waterConsumption < 0)
            {
                errors.Add($"{roomLabel}: chỉ số tháng hiện tại thấp hơn kỳ trước; chưa thể lập hóa đơn.");
                continue;
            }

            var tenantNames = string.Join(", ", contract.ContractTenants
                .OrderByDescending(assignment => assignment.IsPrimaryTenant)
                .ThenBy(assignment => assignment.Tenant.FullName)
                .Select(assignment => assignment.Tenant.FullName.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name)));

            if (string.IsNullOrWhiteSpace(tenantNames))
            {
                tenantNames = "Chưa có thông tin người thuê";
            }

            var electricityAmount = decimal.Round(
                electricityConsumption * config.ElectricityPrice,
                0,
                MidpointRounding.AwayFromZero);
            var waterAmount = decimal.Round(
                waterConsumption * config.WaterPrice,
                0,
                MidpointRounding.AwayFromZero);
            var rentAmount = contract.MonthlyRent;
            // Outstanding debt is entered manually by the manager after checking
            // whether previous payments (including cash) have already been received.
            var previousDebtAmount = 0m;
            var discountAmount = 0m;
            var totalAmount = rentAmount
                + electricityAmount
                + waterAmount
                + config.GarbageFee
                + config.WifiFee
                + previousDebtAmount
                - discountAmount;

            newInvoices.Add(new Invoice
            {
                InvoiceCode = $"HD-{period:yyyyMM}-{contract.ContractId:D4}",
                ContractId = contract.ContractId,
                BillingMonth = period,
                IssuedAt = DateTime.Now,
                RoomLabelSnapshot = roomLabel,
                TenantNamesSnapshot = tenantNames,
                ContractCodeSnapshot = contract.ContractCode,
                RentAmount = rentAmount,
                PreviousElectricityReading = previousReading.ElectricityReading,
                CurrentElectricityReading = currentReading.ElectricityReading,
                ElectricityConsumption = electricityConsumption,
                ElectricityUnitPrice = config.ElectricityPrice,
                ElectricityAmount = electricityAmount,
                PreviousWaterReading = previousReading.WaterReading,
                CurrentWaterReading = currentReading.WaterReading,
                WaterConsumption = waterConsumption,
                WaterUnitPrice = config.WaterPrice,
                WaterAmount = waterAmount,
                GarbageFee = config.GarbageFee,
                WifiFee = config.WifiFee,
                PreviousDebtAmount = previousDebtAmount,
                DiscountAmount = discountAmount,
                TotalAmount = totalAmount,
                Status = "Unpaid"
            });
        }

        if (newInvoices.Count > 0)
        {
            _context.Invoices.AddRange(newInvoices);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Không thể lưu hóa đơn. Có thể dữ liệu đã được thay đổi hoặc hóa đơn đã được lập ở phiên khác; hãy tải lại trang và kiểm tra.";
                return RedirectToAction(nameof(Index), new
                {
                    billingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                });
            }
        }

        if (newInvoices.Count > 0)
        {
            TempData["SuccessMessage"] = $"Đã lập {newInvoices.Count} hóa đơn cho kỳ {period:MM/yyyy}. Tổng giá trị: {newInvoices.Sum(invoice => invoice.TotalAmount):N0} VNĐ."
                + (alreadyCreatedCount > 0 ? $" Bỏ qua {alreadyCreatedCount} hóa đơn đã tồn tại." : string.Empty);
        }
        else if (alreadyCreatedCount > 0 && errors.Count == 0)
        {
            TempData["SuccessMessage"] = $"Các hóa đơn hợp lệ cho kỳ {period:MM/yyyy} đã tồn tại; không phát sinh hóa đơn trùng.";
        }
        else
        {
            TempData["ErrorMessage"] = "Không tạo được hóa đơn mới cho kỳ này.";
        }

        if (errors.Count > 0)
        {
            TempData["GenerationWarnings"] = string.Join(" | ", errors.Take(5))
                + (errors.Count > 5 ? $" | Còn {errors.Count - 5} phòng cần kiểm tra." : string.Empty);
        }

        return RedirectToAction(nameof(Index), new
        {
            billingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var invoice = await _context.Invoices
            .Include(item => item.Contract)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.InvoiceId == id);

        if (invoice is null)
        {
            return NotFound();
        }

        return View(invoice);
    }

    private static decimal CalculateAmountBeforeDiscount(Invoice invoice)
    {
        return invoice.RentAmount
            + invoice.ElectricityAmount
            + invoice.WaterAmount
            + invoice.GarbageFee
            + invoice.WifiFee
            + invoice.PreviousDebtAmount;
    }

    private static DateTime CurrentMonth()
    {
        var today = DateTime.Today;
        return new DateTime(today.Year, today.Month, 1);
    }

    private static bool TryParseBillingMonth(string? value, out DateTime period)
    {
        if (DateTime.TryParseExact(
            value,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed))
        {
            period = new DateTime(parsed.Year, parsed.Month, 1);
            return true;
        }

        period = default;
        return false;
    }
}
