using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class UtilityReadingController : Controller
{
    private readonly ApplicationDbContext _context;

    public UtilityReadingController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? readingMonthFilter, int? roomIdFilter)
    {
        DateTime? selectedMonth = null;
        if (TryParseReadingMonth(readingMonthFilter, out var parsedMonth))
        {
            selectedMonth = parsedMonth;
            readingMonthFilter = parsedMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }
        else
        {
            readingMonthFilter = string.Empty;
        }

        var allReadings = await _context.UtilityReadings
            .Include(reading => reading.Room)
                .ThenInclude(room => room.Building)
            .AsNoTracking()
            .OrderBy(reading => reading.RoomId)
            .ThenBy(reading => reading.ReadingMonth)
            .ThenBy(reading => reading.UtilityReadingId)
            .ToListAsync();

        var previousByRoom = new Dictionary<int, UtilityReading>();
        var rows = new List<UtilityReadingListItemViewModel>();

        foreach (var reading in allReadings)
        {
            previousByRoom.TryGetValue(reading.RoomId, out var previous);

            var matchesMonth = !selectedMonth.HasValue
                || reading.ReadingMonth == selectedMonth.Value;
            var matchesRoom = !roomIdFilter.HasValue
                || reading.RoomId == roomIdFilter.Value;

            if (matchesMonth && matchesRoom)
            {
                rows.Add(new UtilityReadingListItemViewModel
                {
                    UtilityReadingId = reading.UtilityReadingId,
                    BuildingName = reading.Room.Building.BuildingName,
                    RoomCode = reading.Room.RoomCode,
                    ReadingMonth = reading.ReadingMonth,
                    PreviousReadingMonth = previous?.ReadingMonth,
                    PreviousElectricityReading = previous?.ElectricityReading,
                    ElectricityReading = reading.ElectricityReading,
                    ElectricityConsumption = previous is null
                        ? null
                        : reading.ElectricityReading - previous.ElectricityReading,
                    PreviousWaterReading = previous?.WaterReading,
                    WaterReading = reading.WaterReading,
                    WaterConsumption = previous is null
                        ? null
                        : reading.WaterReading - previous.WaterReading,
                    RecordedAt = reading.RecordedAt
                });
            }

            previousByRoom[reading.RoomId] = reading;
        }

        var model = new UtilityReadingListViewModel
        {
            ReadingMonthFilter = readingMonthFilter ?? string.Empty,
            RoomIdFilter = roomIdFilter,
            RoomOptions = await GetRoomOptionsAsync(roomIdFilter),
            Readings = rows
                .OrderByDescending(row => row.ReadingMonth)
                .ThenBy(row => row.BuildingName)
                .ThenBy(row => row.RoomCode)
                .ToList()
        };

        ViewBag.SuccessMessage = TempData["SuccessMessage"];
        ViewBag.ErrorMessage = TempData["ErrorMessage"];
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? roomId, string? readingMonth)
    {
        var roomOptions = await GetRoomOptionsAsync(roomId);
        if (roomOptions.Count > 0 && !roomId.HasValue)
        {
            roomId = int.Parse(roomOptions[0].Value!, CultureInfo.InvariantCulture);
        }
        else if (roomOptions.Count > 0
            && !roomOptions.Any(option => option.Value == roomId!.Value.ToString(CultureInfo.InvariantCulture)))
        {
            roomId = int.Parse(roomOptions[0].Value!, CultureInfo.InvariantCulture);
        }

        if (!TryParseReadingMonth(readingMonth, out var period))
        {
            period = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }

        var model = new UtilityReadingFormViewModel
        {
            RoomId = roomId ?? 0,
            ReadingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };

        await PrepareCreateFormAsync(model.RoomId, period, roomOptions);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UtilityReadingFormViewModel model)
    {
        var hasValidPeriod = TryParseReadingMonth(model.ReadingMonth, out var period);
        if (!hasValidPeriod)
        {
            ModelState.AddModelError(nameof(model.ReadingMonth), "Vui lòng chọn kỳ ghi chỉ số hợp lệ.");
        }
        else if (period > new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1))
        {
            ModelState.AddModelError(nameof(model.ReadingMonth), "Không thể ghi chỉ số cho một kỳ trong tương lai.");
        }

        if (!await _context.Rooms.AnyAsync(room => room.RoomId == model.RoomId))
        {
            ModelState.AddModelError(nameof(model.RoomId), "Phòng được chọn không tồn tại.");
        }

        if (!ModelState.IsValid)
        {
            await PrepareCreateFormAsync(model.RoomId, hasValidPeriod ? period : null);
            return View(model);
        }

        if (await _context.UtilityReadings.AnyAsync(reading =>
            reading.RoomId == model.RoomId && reading.ReadingMonth == period))
        {
            ModelState.AddModelError(string.Empty, "Phòng này đã có chỉ số cho kỳ đã chọn. Hãy sửa bản ghi hiện có thay vì tạo trùng.");
            await PrepareCreateFormAsync(model.RoomId, period);
            return View(model);
        }

        var previous = await FindPreviousReadingAsync(model.RoomId, period);
        var next = await FindNextReadingAsync(model.RoomId, period);
        ValidateReadingSequence(model, period, previous, next);
        ValidateReadingValues(model, previous, next);

        if (!ModelState.IsValid)
        {
            await PrepareCreateFormAsync(model.RoomId, period);
            return View(model);
        }

        var reading = new UtilityReading
        {
            RoomId = model.RoomId,
            ReadingMonth = period,
            ElectricityReading = model.ElectricityReading,
            WaterReading = model.WaterReading,
            RecordedAt = DateTime.Now
        };

        _context.UtilityReadings.Add(reading);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Không thể lưu chỉ số. Có thể kỳ ghi này vừa được tạo ở nơi khác; hãy tải lại trang và kiểm tra lịch sử.");
            await PrepareCreateFormAsync(model.RoomId, period);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã lưu chỉ số điện, nước.";
        return RedirectToAction(nameof(Index), new
        {
            readingMonthFilter = period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            roomIdFilter = reading.RoomId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var reading = await _context.UtilityReadings
            .Include(item => item.Room)
                .ThenInclude(room => room.Building)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UtilityReadingId == id);

        if (reading is null)
        {
            return NotFound();
        }

        var model = new UtilityReadingFormViewModel
        {
            UtilityReadingId = reading.UtilityReadingId,
            RoomId = reading.RoomId,
            ReadingMonth = reading.ReadingMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            ElectricityReading = reading.ElectricityReading,
            WaterReading = reading.WaterReading
        };

        await PrepareEditFormAsync(model, reading);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UtilityReadingFormViewModel model)
    {
        if (id != model.UtilityReadingId)
        {
            return NotFound();
        }

        var reading = await _context.UtilityReadings
            .Include(item => item.Room)
                .ThenInclude(room => room.Building)
            .FirstOrDefaultAsync(item => item.UtilityReadingId == id);

        if (reading is null)
        {
            return NotFound();
        }

        if (model.RoomId != reading.RoomId
            || !TryParseReadingMonth(model.ReadingMonth, out var submittedPeriod)
            || submittedPeriod != reading.ReadingMonth)
        {
            ModelState.AddModelError(string.Empty, "Không thể thay đổi phòng hoặc kỳ của bản ghi. Hãy quay lại danh sách và mở lại bản ghi cần sửa.");
        }

        if (!ModelState.IsValid)
        {
            model.RoomId = reading.RoomId;
            model.ReadingMonth = reading.ReadingMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            ModelState.Remove(nameof(model.RoomId));
            ModelState.Remove(nameof(model.ReadingMonth));
            await PrepareEditFormAsync(model, reading);
            return View(model);
        }

        var previous = await FindPreviousReadingAsync(reading.RoomId, reading.ReadingMonth, reading.UtilityReadingId);
        var next = await FindNextReadingAsync(reading.RoomId, reading.ReadingMonth, reading.UtilityReadingId);
        ValidateReadingValues(model, previous, next);

        if (!ModelState.IsValid)
        {
            model.RoomId = reading.RoomId;
            model.ReadingMonth = reading.ReadingMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            ModelState.Remove(nameof(model.RoomId));
            ModelState.Remove(nameof(model.ReadingMonth));
            await PrepareEditFormAsync(model, reading);
            return View(model);
        }

        reading.ElectricityReading = model.ElectricityReading;
        reading.WaterReading = model.WaterReading;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(string.Empty, "Không thể cập nhật chỉ số. Vui lòng thử lại.");
            await PrepareEditFormAsync(model, reading);
            return View(model);
        }

        TempData["SuccessMessage"] = "Đã cập nhật chỉ số điện, nước. Lượng tiêu thụ của các kỳ sau được tính lại theo chỉ số mới.";
        return RedirectToAction(nameof(Index), new
        {
            readingMonthFilter = reading.ReadingMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            roomIdFilter = reading.RoomId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var reading = await _context.UtilityReadings
            .Include(item => item.Room)
                .ThenInclude(room => room.Building)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UtilityReadingId == id);

        return reading is null ? NotFound() : View(reading);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var reading = await _context.UtilityReadings.FindAsync(id);
        if (reading is null)
        {
            return NotFound();
        }

        var period = reading.ReadingMonth;
        var roomId = reading.RoomId;
        var hasLaterReading = await _context.UtilityReadings.AnyAsync(item =>
            item.RoomId == roomId && item.ReadingMonth > period);

        if (hasLaterReading)
        {
            TempData["ErrorMessage"] = "Không thể xóa vì phòng đã có chỉ số ở kỳ sau. Hãy sửa bản ghi để giữ liên tục lịch sử ghi chỉ số.";
            return RedirectToAction(nameof(Index), new
            {
                readingMonthFilter = period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                roomIdFilter = roomId
            });
        }

        _context.UtilityReadings.Remove(reading);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TempData["ErrorMessage"] = "Không thể xóa bản ghi chỉ số này.";
            return RedirectToAction(nameof(Index), new
            {
                readingMonthFilter = period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                roomIdFilter = roomId
            });
        }

        TempData["SuccessMessage"] = "Đã xóa bản ghi chỉ số. Lượng tiêu thụ của các kỳ sau được tính lại theo dữ liệu còn lại.";
        return RedirectToAction(nameof(Index), new
        {
            readingMonthFilter = period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            roomIdFilter = roomId
        });
    }

    private async Task PrepareCreateFormAsync(
        int roomId,
        DateTime? period,
        List<SelectListItem>? roomOptions = null)
    {
        ViewBag.HasRooms = false;
        ViewBag.RoomOptions = roomOptions ?? await GetRoomOptionsAsync(roomId);
        var options = (List<SelectListItem>)ViewBag.RoomOptions;
        ViewBag.HasRooms = options.Count > 0;

        var selectedRoom = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .FirstOrDefaultAsync(room => room.RoomId == roomId);
        ViewBag.RoomDisplay = selectedRoom is null
            ? string.Empty
            : $"{selectedRoom.Building.BuildingName} - Phòng {selectedRoom.RoomCode}";

        ViewBag.PreviousReading = null;
        ViewBag.NextReading = null;
        ViewBag.ExistingReadingId = null;

        if (period.HasValue && roomId > 0)
        {
            ViewBag.PreviousReading = await FindPreviousReadingAsync(roomId, period.Value);
            ViewBag.NextReading = await FindNextReadingAsync(roomId, period.Value);
            ViewBag.ExistingReadingId = await _context.UtilityReadings
                .Where(reading => reading.RoomId == roomId && reading.ReadingMonth == period.Value)
                .Select(reading => (int?)reading.UtilityReadingId)
                .FirstOrDefaultAsync();
        }
    }

    private async Task PrepareEditFormAsync(
        UtilityReadingFormViewModel model,
        UtilityReading reading)
    {
        ViewBag.RoomDisplay = $"{reading.Room.Building.BuildingName} - Phòng {reading.Room.RoomCode}";
        ViewBag.PreviousReading = await FindPreviousReadingAsync(
            reading.RoomId, reading.ReadingMonth, reading.UtilityReadingId);
        ViewBag.NextReading = await FindNextReadingAsync(
            reading.RoomId, reading.ReadingMonth, reading.UtilityReadingId);
    }

    private async Task<List<SelectListItem>> GetRoomOptionsAsync(int? selectedRoomId)
    {
        var rooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        return rooms.Select(room => new SelectListItem
        {
            Value = room.RoomId.ToString(CultureInfo.InvariantCulture),
            Text = $"{room.Building.BuildingName} - Phòng {room.RoomCode}",
            Selected = selectedRoomId == room.RoomId
        }).ToList();
    }

    private async Task<UtilityReading?> FindPreviousReadingAsync(
        int roomId,
        DateTime period,
        int? excludeReadingId = null)
    {
        var query = _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => reading.RoomId == roomId && reading.ReadingMonth < period);

        if (excludeReadingId.HasValue)
        {
            query = query.Where(reading => reading.UtilityReadingId != excludeReadingId.Value);
        }

        return await query
            .OrderByDescending(reading => reading.ReadingMonth)
            .ThenByDescending(reading => reading.UtilityReadingId)
            .FirstOrDefaultAsync();
    }

    private async Task<UtilityReading?> FindNextReadingAsync(
        int roomId,
        DateTime period,
        int? excludeReadingId = null)
    {
        var query = _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => reading.RoomId == roomId && reading.ReadingMonth > period);

        if (excludeReadingId.HasValue)
        {
            query = query.Where(reading => reading.UtilityReadingId != excludeReadingId.Value);
        }

        return await query
            .OrderBy(reading => reading.ReadingMonth)
            .ThenBy(reading => reading.UtilityReadingId)
            .FirstOrDefaultAsync();
    }

    private void ValidateReadingSequence(
        UtilityReadingFormViewModel model,
        DateTime period,
        UtilityReading? previous,
        UtilityReading? next)
    {
        if (previous is not null)
        {
            var expectedPeriod = previous.ReadingMonth.AddMonths(1);
            if (expectedPeriod != period)
            {
                ModelState.AddModelError(
                    nameof(model.ReadingMonth),
                    $"Hãy ghi kỳ còn thiếu {expectedPeriod:MM/yyyy} trước khi ghi kỳ {period:MM/yyyy}.");
            }

            return;
        }

        if (next is not null && period.AddMonths(1) != next.ReadingMonth)
        {
            var expectedPeriod = next.ReadingMonth.AddMonths(-1);
            ModelState.AddModelError(
                nameof(model.ReadingMonth),
                $"Để giữ lịch sử liên tục, hãy ghi kỳ {expectedPeriod:MM/yyyy} ngay trước kỳ {next.ReadingMonth:MM/yyyy}.");
        }
    }

    private void ValidateReadingValues(
        UtilityReadingFormViewModel model,
        UtilityReading? previous,
        UtilityReading? next)
    {
        if (previous is not null)
        {
            if (model.ElectricityReading < previous.ElectricityReading)
            {
                ModelState.AddModelError(
                    nameof(model.ElectricityReading),
                    $"Chỉ số điện mới không được nhỏ hơn chỉ số kỳ {previous.ReadingMonth:MM/yyyy} ({previous.ElectricityReading:N2}).");
            }

            if (model.WaterReading < previous.WaterReading)
            {
                ModelState.AddModelError(
                    nameof(model.WaterReading),
                    $"Chỉ số nước mới không được nhỏ hơn chỉ số kỳ {previous.ReadingMonth:MM/yyyy} ({previous.WaterReading:N2}).");
            }
        }

        if (next is not null)
        {
            if (model.ElectricityReading > next.ElectricityReading)
            {
                ModelState.AddModelError(
                    nameof(model.ElectricityReading),
                    $"Chỉ số điện không được lớn hơn bản ghi kỳ {next.ReadingMonth:MM/yyyy} ({next.ElectricityReading:N2}) để tránh tạo lượng tiêu thụ âm ở kỳ sau.");
            }

            if (model.WaterReading > next.WaterReading)
            {
                ModelState.AddModelError(
                    nameof(model.WaterReading),
                    $"Chỉ số nước không được lớn hơn bản ghi kỳ {next.ReadingMonth:MM/yyyy} ({next.WaterReading:N2}) để tránh tạo lượng tiêu thụ âm ở kỳ sau.");
            }
        }
    }

    private static bool TryParseReadingMonth(string? value, out DateTime period)
    {
        period = default;

        if (value is null
            || value.Length != 7
            || value[4] != '-'
            || !int.TryParse(value.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(value.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var month)
            || year < 1
            || month < 1
            || month > 12)
        {
            return false;
        }

        period = new DateTime(year, month, 1);
        return true;
    }
}
