using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(
        ILogger<HomeController> logger,
        ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;

        var model = new HomeDashboardViewModel
        {
            BuildingCount = await _context.Buildings
                .AsNoTracking()
                .CountAsync(),

            RoomCount = await _context.Rooms
                .AsNoTracking()
                .CountAsync(),

            AvailableRoomCount = await _context.Rooms
                .AsNoTracking()
                .CountAsync(room =>
                    room.Status != "Maintenance"
                    && !_context.RentalContracts.Any(contract =>
                        contract.RoomId == room.RoomId
                        && contract.Status == "Active"
                        && contract.StartDate.Date <= today
                        && contract.EndDate.Date >= today)),

            OccupiedRoomCount = await _context.Rooms
                .AsNoTracking()
                .CountAsync(room =>
                    room.Status != "Maintenance"
                    && _context.RentalContracts.Any(contract =>
                        contract.RoomId == room.RoomId
                        && contract.Status == "Active"
                        && contract.StartDate.Date <= today
                        && contract.EndDate.Date >= today)),

            TenantCount = await _context.TenantProfiles
                .AsNoTracking()
                .CountAsync(),

            ActiveContractCount = await _context.RentalContracts
                .AsNoTracking()
                .CountAsync(contract =>
                    contract.Status == "Active"
                    && contract.StartDate.Date <= today
                    && contract.EndDate.Date >= today)
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
