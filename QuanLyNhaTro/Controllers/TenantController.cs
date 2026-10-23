using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

public class TenantController : Controller
{
    private readonly ApplicationDbContext _context;

    public TenantController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var tenants = await _context.TenantProfiles
            .AsNoTracking()
            .OrderBy(tenant => tenant.FullName)
            .ToListAsync();

        var today = DateTime.Today;
        var currentlyRentingTenantCount = await _context.ContractTenants
            .Where(assignment => assignment.Contract.Status == "Active"
                && assignment.Contract.StartDate.Date <= today
                && assignment.Contract.EndDate.Date >= today)
            .Select(assignment => assignment.TenantId)
            .Distinct()
            .CountAsync();

        ViewBag.TotalTenants = tenants.Count;
        ViewBag.CurrentlyRentingTenants = currentlyRentingTenantCount;
        ViewBag.NotCurrentlyRentingTenants = tenants.Count - currentlyRentingTenantCount;

        return View(tenants);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var tenant = await _context.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == id);

        if (tenant is null)
        {
            return NotFound();
        }

        return View(tenant);
    }

    public IActionResult Create()
    {
        return View(new TenantProfile());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("FullName,Phone,IdentityNumber,DateOfBirth,BirthYear,PermanentAddress,Notes")] TenantProfile tenant)
    {
        NormalizeTenant(tenant);
        LocalizeBirthYearBindingError();
        RemoveUnpostedModelStateEntries();
        ValidateTenant(tenant);

        if (!ModelState.IsValid)
        {
            return View(tenant);
        }

        _context.TenantProfiles.Add(tenant);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Đã thêm người thuê thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var tenant = await _context.TenantProfiles.FindAsync(id);

        if (tenant is null)
        {
            return NotFound();
        }

        return View(tenant);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("TenantId,FullName,Phone,IdentityNumber,DateOfBirth,BirthYear,PermanentAddress,Notes")] TenantProfile tenant)
    {
        if (id != tenant.TenantId)
        {
            return NotFound();
        }

        NormalizeTenant(tenant);
        LocalizeBirthYearBindingError();
        RemoveUnpostedModelStateEntries();
        ValidateTenant(tenant);

        if (!ModelState.IsValid)
        {
            return View(tenant);
        }

        var existingTenant = await _context.TenantProfiles.FindAsync(id);

        if (existingTenant is null)
        {
            return NotFound();
        }

        existingTenant.FullName = tenant.FullName;
        existingTenant.Phone = tenant.Phone;
        existingTenant.IdentityNumber = tenant.IdentityNumber;
        existingTenant.DateOfBirth = tenant.DateOfBirth;
        existingTenant.BirthYear = tenant.BirthYear;
        existingTenant.PermanentAddress = tenant.PermanentAddress;
        existingTenant.Notes = tenant.Notes;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await TenantExistsAsync(id))
            {
                return NotFound();
            }

            ModelState.AddModelError(string.Empty, "Dữ liệu người thuê đã được thay đổi bởi người khác. Vui lòng tải lại trang và thử lại.");
            return View(tenant);
        }

        TempData["SuccessMessage"] = "Đã cập nhật người thuê thành công.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var tenant = await _context.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == id);

        if (tenant is null)
        {
            return NotFound();
        }

        return View(tenant);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var tenant = await _context.TenantProfiles.FindAsync(id);

        if (tenant is null)
        {
            return NotFound();
        }

        var hasContracts = await _context.ContractTenants
            .AsNoTracking()
            .AnyAsync(assignment => assignment.TenantId == id);

        if (hasContracts)
        {
            TempData["ErrorMessage"] = "Không thể xóa người thuê vì người thuê đang được tham chiếu bởi hợp đồng thuê.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        try
        {
            _context.TenantProfiles.Remove(tenant);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Không thể xóa người thuê vì dữ liệu vẫn đang được tham chiếu.";
            return RedirectToAction(nameof(Delete), new { id });
        }

        TempData["SuccessMessage"] = "Đã xóa người thuê thành công.";
        return RedirectToAction(nameof(Index));
    }

    private static void NormalizeTenant(TenantProfile tenant)
    {
        tenant.FullName = tenant.FullName?.Trim() ?? string.Empty;
        tenant.Phone = NormalizeOptionalText(tenant.Phone);
        tenant.IdentityNumber = NormalizeOptionalText(tenant.IdentityNumber);
        tenant.PermanentAddress = NormalizeOptionalText(tenant.PermanentAddress);
        tenant.Notes = NormalizeOptionalText(tenant.Notes);
    }

    private void ValidateTenant(TenantProfile tenant)
    {
        if (tenant.DateOfBirth.HasValue && tenant.DateOfBirth.Value.Date > DateTime.Today)
        {
            ModelState.AddModelError(nameof(TenantProfile.DateOfBirth), "Ngày sinh không được lớn hơn ngày hiện tại.");
        }

        if (tenant.BirthYear.HasValue
            && (tenant.BirthYear.Value < 1 || tenant.BirthYear.Value > DateTime.Today.Year))
        {
            ModelState.AddModelError(nameof(TenantProfile.BirthYear),
                $"Năm sinh phải nằm trong khoảng từ 1 đến {DateTime.Today.Year}.");
        }

        if (tenant.DateOfBirth.HasValue && tenant.BirthYear.HasValue)
        {
            ModelState.AddModelError(nameof(TenantProfile.BirthYear),
                "Chỉ nhập ngày sinh đầy đủ hoặc năm sinh, không nhập đồng thời cả hai.");
        }
    }

    private void RemoveUnpostedModelStateEntries()
    {
        ModelState.Remove(nameof(TenantProfile.ContractTenants));
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalizedValue = value?.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }

    private async Task<bool> TenantExistsAsync(int id)
    {
        return await _context.TenantProfiles.AnyAsync(tenant => tenant.TenantId == id);
    }
    private void LocalizeBirthYearBindingError()
    {
        const string field = nameof(TenantProfile.BirthYear);

        if (!ModelState.TryGetValue(field, out var entry)
            || entry.Errors.Count == 0)
        {
            return;
        }

        var attemptedValue = entry.AttemptedValue;

        ModelState.Remove(field);
        ModelState.SetModelValue(
            field, attemptedValue, attemptedValue);

        ModelState.AddModelError(
            field,
            "Năm sinh phải là số nguyên hợp lệ.");
    }
}
