using System.Globalization;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;
using QuanLyNhaTro.ViewModels;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class UtilityReadingController : Controller
{
    private readonly ApplicationDbContext _context;

    public UtilityReadingController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index(string? readingMonth, string? readingMonthFilter = null, int? roomIdFilter = null)
    {
        var requestedMonth = readingMonth ?? readingMonthFilter;
        if (!TryParseReadingMonth(requestedMonth, out var period))
        {
            period = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }

        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (period > currentMonth)
        {
            period = currentMonth;
            TempData["ErrorMessage"] = "Chưa thể nhập chỉ số cho kỳ trong tương lai.";
        }

        var model = await BuildBatchViewModelAsync(period);
        ViewBag.SuccessMessage = TempData["SuccessMessage"];
        ViewBag.ErrorMessage = TempData["ErrorMessage"];
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMonth(UtilityReadingBatchViewModel model)
    {
        if (!TryParseReadingMonth(model.ReadingMonth, out var period))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn kỳ ghi chỉ số hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (period > currentMonth)
        {
            TempData["ErrorMessage"] = "Chưa thể nhập chỉ số cho kỳ trong tương lai.";
            return RedirectToAction(nameof(Index), new { readingMonth = currentMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture) });
        }

        var allRooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        var roomIds = allRooms.Select(room => room.RoomId).ToHashSet();
        var submittedRows = (model.Rooms ?? new List<UtilityReadingBatchRowViewModel>())
            .Where(row => roomIds.Contains(row.RoomId))
            .GroupBy(row => row.RoomId)
            .ToDictionary(group => group.Key, group => group.First());

        var allReadings = await _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => roomIds.Contains(reading.RoomId))
            .OrderBy(reading => reading.ReadingMonth)
            .ToListAsync();

        var currentByRoom = allReadings
            .Where(reading => reading.ReadingMonth == period)
            .ToDictionary(reading => reading.RoomId);

        var savedRoomIds = new HashSet<int>();
        var changedReadings = new List<UtilityReading>();
        var errorCount = 0;

        foreach (var room in allRooms)
        {
            if (!submittedRows.TryGetValue(room.RoomId, out var row))
            {
                continue;
            }

            row.ErrorMessage = null;
            row.WasSavedThisRequest = false;

            if (row.ElectricityReading is not decimal electricityReading
                || row.WaterReading is not decimal waterReading)
            {
                if (row.ElectricityReading is null && row.WaterReading is null)
                {
                    continue;
                }

                row.ErrorMessage = "Cần nhập đủ cả chỉ số điện và nước hoặc để trống cả hai ô.";
                errorCount++;
                continue;
            }

            var previous = allReadings
                .Where(reading => reading.RoomId == room.RoomId && reading.ReadingMonth < period)
                .OrderByDescending(reading => reading.ReadingMonth)
                .FirstOrDefault();
            var next = allReadings
                .Where(reading => reading.RoomId == room.RoomId && reading.ReadingMonth > period)
                .OrderBy(reading => reading.ReadingMonth)
                .FirstOrDefault();
            var existing = currentByRoom.GetValueOrDefault(room.RoomId);

            if (electricityReading < 0 || waterReading < 0)
            {
                row.ErrorMessage = "Chỉ số không được là số âm.";
            }
            else if (previous is not null && previous.ReadingMonth.AddMonths(1) != period && existing is null)
            {
                row.ErrorMessage = $"Cần ghi kỳ {previous.ReadingMonth.AddMonths(1):MM/yyyy} trước kỳ này.";
            }
            else if (previous is null && next is not null && period.AddMonths(1) != next.ReadingMonth && existing is null)
            {
                row.ErrorMessage = $"Cần ghi kỳ {next.ReadingMonth.AddMonths(-1):MM/yyyy} trước kỳ {next.ReadingMonth:MM/yyyy}.";
            }
            else if (previous is not null
                && (electricityReading < previous.ElectricityReading
                    || waterReading < previous.WaterReading))
            {
                row.ErrorMessage = $"Chỉ số mới phải lớn hơn hoặc bằng chỉ số kỳ {previous.ReadingMonth:MM/yyyy}.";
            }
            else if (next is not null
                && (electricityReading > next.ElectricityReading
                    || waterReading > next.WaterReading))
            {
                row.ErrorMessage = $"Chỉ số mới không được lớn hơn chỉ số kỳ sau ({next.ReadingMonth:MM/yyyy}).";
            }

            if (row.ErrorMessage is not null)
            {
                errorCount++;
                continue;
            }

            if (existing is null)
            {
                existing = new UtilityReading
                {
                    RoomId = room.RoomId,
                    ReadingMonth = period,
                    ElectricityReading = electricityReading,
                    WaterReading = waterReading,
                    RecordedAt = DateTime.Now
                };
                _context.UtilityReadings.Add(existing);
                currentByRoom[room.RoomId] = existing;
                allReadings.Add(existing);
            }
            else
            {
                existing.ElectricityReading = electricityReading;
                existing.WaterReading = waterReading;
                existing.RecordedAt = DateTime.Now;
                _context.UtilityReadings.Update(existing);
            }

            changedReadings.Add(existing);
            savedRoomIds.Add(room.RoomId);
            row.WasSavedThisRequest = true;
        }

        if (changedReadings.Count > 0)
        {
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                foreach (var row in submittedRows.Values.Where(row => savedRoomIds.Contains(row.RoomId)))
                {
                    row.ErrorMessage = "Không thể lưu dòng này do lỗi cơ sở dữ liệu. Hãy tải lại trang và kiểm tra dữ liệu.";
                    row.WasSavedThisRequest = false;
                }

                errorCount += savedRoomIds.Count;
                savedRoomIds.Clear();
            }
        }

        model = await BuildBatchViewModelAsync(period, submittedRows, savedRoomIds);
        model.StatusMessage = savedRoomIds.Count > 0
            ? $"Đã lưu {savedRoomIds.Count} phòng. " +
              (errorCount > 0 ? $"Còn {errorCount} dòng chưa lưu do thiếu hoặc sai dữ liệu." : "Các dòng đã nhập đều hợp lệ.")
            : errorCount > 0
                ? $"Chưa lưu được dòng nào. Có {errorCount} dòng cần bổ sung hoặc sửa dữ liệu."
                : "Không có thay đổi nào cần lưu.";

        ViewBag.SuccessMessage = null;
        ViewBag.ErrorMessage = null;
        return View("Index", model);
    }

    private async Task<UtilityReadingBatchViewModel> BuildBatchViewModelAsync(
        DateTime period,
        Dictionary<int, UtilityReadingBatchRowViewModel>? submittedRows = null,
        HashSet<int>? savedRoomIds = null)
    {
        var rooms = await _context.Rooms
            .Include(room => room.Building)
            .AsNoTracking()
            .OrderBy(room => room.Building.BuildingName)
            .ThenBy(room => room.RoomCode)
            .ToListAsync();

        var roomIds = rooms.Select(room => room.RoomId).ToHashSet();
        var readings = await _context.UtilityReadings
            .AsNoTracking()
            .Where(reading => roomIds.Contains(reading.RoomId))
            .OrderBy(reading => reading.ReadingMonth)
            .ToListAsync();

        var rows = new List<UtilityReadingBatchRowViewModel>();
        foreach (var room in rooms)
        {
            var current = readings
                .Where(reading => reading.RoomId == room.RoomId && reading.ReadingMonth == period)
                .FirstOrDefault();
            var previous = readings
                .Where(reading => reading.RoomId == room.RoomId && reading.ReadingMonth < period)
                .OrderByDescending(reading => reading.ReadingMonth)
                .FirstOrDefault();

            UtilityReadingBatchRowViewModel? submitted = null;
            var hasSubmitted = submittedRows is not null
                && submittedRows.TryGetValue(room.RoomId, out submitted);

            rows.Add(new UtilityReadingBatchRowViewModel
            {
                RoomId = room.RoomId,
                RoomLabel = $"{room.Building.BuildingName} - Phòng {room.RoomCode}",
                CurrentReadingId = current?.UtilityReadingId,
                PreviousReadingMonth = previous?.ReadingMonth,
                PreviousElectricityReading = previous?.ElectricityReading,
                PreviousWaterReading = previous?.WaterReading,
                ElectricityReading = current is not null
                    ? current.ElectricityReading
                    : hasSubmitted ? submitted!.ElectricityReading : null,
                WaterReading = current is not null
                    ? current.WaterReading
                    : hasSubmitted ? submitted!.WaterReading : null,
                ErrorMessage = hasSubmitted ? submitted!.ErrorMessage : null,
                WasSavedThisRequest = savedRoomIds?.Contains(room.RoomId) == true,
                HasCurrentReading = current is not null
            });
        }

        return new UtilityReadingBatchViewModel
        {
            ReadingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            Rooms = rows,
            CurrentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };
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

        if (model.RoomId > 0
            && !await _context.Rooms.AnyAsync(room => room.RoomId == model.RoomId))
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

    [HttpGet]
    public async Task<IActionResult> DownloadTemplate(string? readingMonth)
    {
        if (!TryParseReadingMonth(readingMonth, out var period))
        {
            period = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }

        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (period > currentMonth)
        {
            TempData["ErrorMessage"] = "Chưa thể tạo mẫu nhập cho kỳ trong tương lai.";
            return RedirectToAction(nameof(Index));
        }

        var model = await BuildBatchViewModelAsync(period);
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("ChiSoDienNuoc");

        worksheet.Cell(1, 1).Value = "RoomId";
        worksheet.Cell(1, 2).Value = "Phòng";
        worksheet.Cell(1, 3).Value = "Điện kỳ trước";
        worksheet.Cell(1, 4).Value = "Điện kỳ này";
        worksheet.Cell(1, 5).Value = "Nước kỳ trước";
        worksheet.Cell(1, 6).Value = "Nước kỳ này";

        for (var index = 0; index < model.Rooms.Count; index++)
        {
            var row = model.Rooms[index];
            var excelRow = index + 2;
            worksheet.Cell(excelRow, 1).Value = row.RoomId;
            worksheet.Cell(excelRow, 2).Value = row.RoomLabel;
            if (row.PreviousElectricityReading.HasValue)
            {
                worksheet.Cell(excelRow, 3).Value = (double)row.PreviousElectricityReading.Value;
            }
            if (row.ElectricityReading.HasValue)
            {
                worksheet.Cell(excelRow, 4).Value = (double)electricityReading;
            }
            if (row.PreviousWaterReading.HasValue)
            {
                worksheet.Cell(excelRow, 5).Value = (double)row.PreviousWaterReading.Value;
            }
            if (row.WaterReading.HasValue)
            {
                worksheet.Cell(excelRow, 6).Value = (double)waterReading;
            }
        }

        var header = worksheet.Range(1, 1, 1, 6);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.LightGray;
        worksheet.SheetView.FreezeRows(1);
        worksheet.Columns(1, 6).AdjustToContents();
        worksheet.Column(2).Width = Math.Max(worksheet.Column(2).Width, 28);
        worksheet.Column(1).Style.Protection.Locked = true;
        worksheet.Column(2).Style.Protection.Locked = true;
        worksheet.Column(3).Style.Protection.Locked = true;
        worksheet.Column(5).Style.Protection.Locked = true;
        worksheet.Column(4).Style.Protection.Locked = false;
        worksheet.Column(6).Style.Protection.Locked = false;
        worksheet.Protect();

        worksheet.Cell(model.Rooms.Count + 4, 1).Value =
            "Chỉ sửa cột Điện kỳ này và Nước kỳ này. Không thay đổi RoomId hoặc thứ tự/cấu trúc cột.";
        worksheet.Range(model.Rooms.Count + 4, 1, model.Rooms.Count + 4, 6).Merge();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var fileName = $"Mau_ChiSoDienNuoc_{period:yyyyMM}.xlsx";
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> ImportExcel(IFormFile? file, string? readingMonth)
    {
        if (!TryParseReadingMonth(readingMonth, out var period))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn kỳ ghi chỉ số hợp lệ trước khi nhập Excel.";
            return RedirectToAction(nameof(Index));
        }

        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (period > currentMonth)
        {
            TempData["ErrorMessage"] = "Không thể import chỉ số cho kỳ trong tương lai.";
            return RedirectToAction(nameof(Index), new
            {
                readingMonth = currentMonth.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn file Excel có dữ liệu.";
            return RedirectToAction(nameof(Index), new
            {
                readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            TempData["ErrorMessage"] = "File Excel không được vượt quá 5 MB.";
            return RedirectToAction(nameof(Index), new
            {
                readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            TempData["ErrorMessage"] = "Chỉ hỗ trợ file Excel định dạng .xlsx.";
            return RedirectToAction(nameof(Index), new
            {
                readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
            {
                TempData["ErrorMessage"] = "File Excel không có trang tính để đọc.";
                return RedirectToAction(nameof(Index), new
                {
                    readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                });
            }

            var expectedHeaders = new[]
            {
                "RoomId", "Phòng", "Điện kỳ trước", "Điện kỳ này", "Nước kỳ trước", "Nước kỳ này"
            };
            for (var column = 1; column <= expectedHeaders.Length; column++)
            {
                var actualHeader = worksheet.Cell(1, column).GetString().Trim();
                if (!string.Equals(actualHeader, expectedHeaders[column - 1], StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Cấu trúc file không đúng mẫu. Hãy tải file mẫu mới từ màn hình nhập chỉ số điện nước.";
                    return RedirectToAction(nameof(Index), new
                    {
                        readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                    });
                }
            }

            var batchModel = await BuildBatchViewModelAsync(period);
            var knownRooms = batchModel.Rooms.ToDictionary(row => row.RoomId);
            var importedRows = new List<UtilityReadingBatchRowViewModel>();
            var importedRoomIds = new HashSet<int>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

            for (var excelRow = 2; excelRow <= lastRow; excelRow++)
            {
                var roomIdCell = worksheet.Cell(excelRow, 1);
                if (roomIdCell.IsEmpty())
                {
                    continue;
                }

                if (!TryReadExcelInteger(roomIdCell, out var roomId) || roomId <= 0)
                {
                    TempData["ErrorMessage"] = $"RoomId tại dòng {excelRow} không hợp lệ.";
                    return RedirectToAction(nameof(Index), new
                    {
                        readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                    });
                }

                if (!importedRoomIds.Add(roomId))
                {
                    TempData["ErrorMessage"] = $"Phòng có mã RoomId {roomId} bị lặp tại file Excel.";
                    return RedirectToAction(nameof(Index), new
                    {
                        readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                    });
                }

                if (!knownRooms.TryGetValue(roomId, out var knownRoom))
                {
                    TempData["ErrorMessage"] = $"RoomId {roomId} tại dòng {excelRow} không tồn tại trong hệ thống.";
                    return RedirectToAction(nameof(Index), new
                    {
                        readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                    });
                }

                if (!TryReadExcelDecimal(worksheet.Cell(excelRow, 4), out var electricity)
                    || !TryReadExcelDecimal(worksheet.Cell(excelRow, 6), out var water))
                {
                    TempData["ErrorMessage"] = $"Chỉ số điện hoặc nước tại dòng {excelRow} không phải số hợp lệ.";
                    return RedirectToAction(nameof(Index), new
                    {
                        readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                    });
                }

                if (!electricity.HasValue && !water.HasValue)
                {
                    continue;
                }

                importedRows.Add(new UtilityReadingBatchRowViewModel
                {
                    RoomId = roomId,
                    RoomLabel = knownRoom.RoomLabel,
                    PreviousReadingMonth = knownRoom.PreviousReadingMonth,
                    PreviousElectricityReading = knownRoom.PreviousElectricityReading,
                    PreviousWaterReading = knownRoom.PreviousWaterReading,
                    ElectricityReading = electricity,
                    WaterReading = water
                });
            }

            if (importedRows.Count == 0)
            {
                TempData["ErrorMessage"] = "File không có dòng nào chứa chỉ số điện/nước cần nhập.";
                return RedirectToAction(nameof(Index), new
                {
                    readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                });
            }

            return await SaveMonth(new UtilityReadingBatchViewModel
            {
                ReadingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Rooms = importedRows
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TempData["ErrorMessage"] = "Không thể đọc file Excel. Hãy kiểm tra file có phải định dạng .xlsx hợp lệ và không bị hỏng.";
            return RedirectToAction(nameof(Index), new
            {
                readingMonth = period.ToString("yyyy-MM", CultureInfo.InvariantCulture)
            });
        }
    }

    private static bool TryReadExcelInteger(IXLCell cell, out int value)
    {
        if (cell.TryGetValue<int>(out value))
        {
            return true;
        }

        return int.TryParse(cell.GetString().Trim(), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out value);
    }

    private static bool TryReadExcelDecimal(IXLCell cell, out decimal? value)
    {
        value = null;
        if (cell.IsEmpty())
        {
            return true;
        }

        if (cell.TryGetValue<double>(out var number) && double.IsFinite(number))
        {
            try
            {
                value = Convert.ToDecimal(number, CultureInfo.InvariantCulture);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        var text = cell.GetString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariantValue)
            || decimal.TryParse(text, NumberStyles.Number, CultureInfo.CurrentCulture, out invariantValue))
        {
            value = invariantValue;
            return true;
        }

        return false;
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
