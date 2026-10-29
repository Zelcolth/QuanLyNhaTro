using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;
using QuanLyNhaTro.ViewModels;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class SystemConfigController : Controller
{
    private readonly ApplicationDbContext _context;

    public SystemConfigController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var config = await _context.SystemConfigs
            .AsNoTracking()
            .OrderBy(item => item.ConfigId)
            .FirstOrDefaultAsync();

        return View(config is null
            ? new SystemConfigViewModel()
            : MapViewModel(config));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SystemConfigViewModel model)
    {
        Normalize(model);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var config = await _context.SystemConfigs
            .OrderBy(item => item.ConfigId)
            .FirstOrDefaultAsync();

        if (config is null)
        {
            config = new SystemConfig();
            _context.SystemConfigs.Add(config);
        }

        config.ElectricityPrice = model.ElectricityPrice;
        config.WaterPrice = model.WaterPrice;
        config.GarbageFee = model.GarbageFee;
        config.WifiFee = model.WifiFee;
        config.BankCode = model.BankCode;
        config.BankAccount = model.BankAccount;
        config.AccountName = model.AccountName;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(
                string.Empty,
                "Không thể lưu cấu hình. Vui lòng kiểm tra dữ liệu và thử lại.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã lưu cấu hình hệ thống.";
        return RedirectToAction(nameof(Index));
    }

    private static SystemConfigViewModel MapViewModel(SystemConfig config)
    {
        return new SystemConfigViewModel
        {
            ElectricityPrice = config.ElectricityPrice,
            WaterPrice = config.WaterPrice,
            GarbageFee = config.GarbageFee,
            WifiFee = config.WifiFee,
            BankCode = config.BankCode,
            BankAccount = config.BankAccount,
            AccountName = config.AccountName
        };
    }

    private static void Normalize(SystemConfigViewModel model)
    {
        model.BankCode = NormalizeOptionalText(model.BankCode);
        model.BankAccount = NormalizeOptionalText(model.BankAccount);
        model.AccountName = NormalizeOptionalText(model.AccountName);
    }

    private static string? NormalizeOptionalText(string? value)
    {
        var normalizedValue = value?.Trim();
        return string.IsNullOrWhiteSpace(normalizedValue) ? null : normalizedValue;
    }
}
