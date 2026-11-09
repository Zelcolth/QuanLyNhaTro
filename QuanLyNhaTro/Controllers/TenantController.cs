using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Data;
using QuanLyNhaTro.Models;
using QuanLyNhaTro.ViewModels;

namespace QuanLyNhaTro.Controllers;

[Authorize]
public class TenantController : Controller
{
    private const long MaxDocumentFileSizeBytes = 5 * 1024 * 1024;
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public TenantController(
        ApplicationDbContext context,
        IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    public async Task<IActionResult> Index()
    {
        var tenants = await _context.TenantProfiles
            .AsNoTracking()
            .OrderBy(tenant => tenant.FullName)
            .ToListAsync();

        var today = DateTime.Today;
        var linkedTenantIds = await _context.ContractTenants
            .AsNoTracking()
            .Select(assignment => assignment.TenantId)
            .Distinct()
            .ToListAsync();
        var currentlyRentingTenantIds = await _context.ContractTenants
            .AsNoTracking()
            .Where(assignment => assignment.IsActive
                && assignment.Contract.Status == "Active"
                && assignment.Contract.StartDate.Date <= today
                && assignment.Contract.EndDate.Date >= today)
            .Select(assignment => assignment.TenantId)
            .Distinct()
            .ToListAsync();
        var previouslyRentedTenantIds = await _context.ContractTenants
            .AsNoTracking()
            .Where(assignment => assignment.Contract.Status != "Draft"
                && assignment.Contract.Status != "Cancelled")
            .Select(assignment => assignment.TenantId)
            .Distinct()
            .ToListAsync();

        var formerTenantIds = previouslyRentedTenantIds
            .Except(currentlyRentingTenantIds)
            .ToHashSet();

        ViewBag.TotalTenants = tenants.Count;
        ViewBag.CurrentlyRentingTenants = currentlyRentingTenantIds.Count;
        ViewBag.FormerTenants = formerTenantIds.Count;
        ViewBag.FormerTenantIds = formerTenantIds;
        ViewBag.LinkedTenantIds = linkedTenantIds.ToHashSet();
        ViewBag.CurrentlyRentingTenantIds = currentlyRentingTenantIds.ToHashSet();

        return View(tenants);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id is null)
        {
            return NotFound();
        }

        var tenant = await _context.TenantProfiles
            .Include(item => item.ContractTenants)
                .ThenInclude(assignment => assignment.Contract)
                    .ThenInclude(contract => contract.Room)
                        .ThenInclude(room => room.Building)
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == id);

        if (tenant is null)
        {
            return NotFound();
        }

        ViewBag.ContractHistory = tenant.ContractTenants
            .OrderByDescending(assignment => assignment.Contract.StartDate)
            .ToList();

        return View(tenant);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxDocumentFileSizeBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxDocumentFileSizeBytes + 64 * 1024)]
    public async Task<IActionResult> UploadDocument(
        int id,
        string? documentType,
        IFormFile? file)
    {
        var normalizedDocumentType = NormalizeDocumentType(documentType);
        if (normalizedDocumentType is null)
        {
            return BadRequest();
        }

        var tenant = await _context.TenantProfiles.FindAsync(id);
        if (tenant is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ảnh cần tải lên.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (file.Length > MaxDocumentFileSizeBytes)
        {
            TempData["ErrorMessage"] = "Dung lượng mỗi ảnh không được vượt quá 5 MB.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var extension = await ValidateDocumentImageAsync(file);
        if (extension is null)
        {
            TempData["ErrorMessage"] = "Ảnh phải đúng định dạng JPG, JPEG hoặc PNG. Không hỗ trợ file đổi đuôi giả ảnh.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var storageDirectory = GetTenantDocumentStorageDirectory();
        var newFileName = $"{Guid.NewGuid():N}{extension}";
        var newFilePath = Path.Combine(storageDirectory, newFileName);

        try
        {
            Directory.CreateDirectory(storageDirectory);

            await using var destination = new FileStream(
                newFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);

            await file.CopyToAsync(destination);
        }
        catch (IOException)
        {
            TryDeleteTenantDocument(newFileName);
            TempData["ErrorMessage"] = "Không thể lưu ảnh vào máy chủ. Vui lòng thử lại.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (UnauthorizedAccessException)
        {
            TryDeleteTenantDocument(newFileName);
            TempData["ErrorMessage"] = "Ứng dụng không có quyền lưu ảnh. Vui lòng kiểm tra quyền truy cập thư mục lưu trữ.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var oldFileName = GetTenantDocumentFileName(tenant, normalizedDocumentType);
        SetTenantDocumentFileName(tenant, normalizedDocumentType, newFileName);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            TryDeleteTenantDocument(newFileName);
            TempData["ErrorMessage"] = "Không thể cập nhật hồ sơ giấy tờ. Ảnh cũ vẫn được giữ nguyên.";
            return RedirectToAction(nameof(Details), new { id });
        }

        TryDeleteTenantDocument(oldFileName);
        TempData["SuccessMessage"] = "Đã tải ảnh giấy tờ lên thành công.";
        return RedirectToAction(nameof(Details), new { id });
    }


    [HttpGet]
    public Task<IActionResult> ViewDocument(int id, string? documentType)
    {
        return GetTenantDocumentAsync(id, documentType, asAttachment: false);
    }

    [HttpGet]
    public Task<IActionResult> DownloadDocument(int id, string? documentType)
    {
        return GetTenantDocumentAsync(id, documentType, asAttachment: true);
    }

    private async Task<IActionResult> GetTenantDocumentAsync(
        int id,
        string? documentType,
        bool asAttachment)
    {
        var normalizedDocumentType = NormalizeDocumentType(documentType);
        if (normalizedDocumentType is null)
        {
            return BadRequest();
        }

        var tenant = await _context.TenantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantId == id);

        if (tenant is null)
        {
            return NotFound();
        }

        var fileName = GetTenantDocumentFileName(tenant, normalizedDocumentType);
        if (!IsGeneratedTenantDocumentFileName(fileName))
        {
            return NotFound();
        }

        var filePath = Path.Combine(GetTenantDocumentStorageDirectory(), fileName!);
        if (!System.IO.File.Exists(filePath))
        {
            return NotFound();
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var contentType = extension == ".png" ? "image/png" : "image/jpeg";
        var downloadName = GetTenantDocumentDownloadName(
            id, normalizedDocumentType, extension);

        Response.Headers["Cache-Control"] = "private, no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";
        Response.Headers["X-Content-Type-Options"] = "nosniff";

        FileStream stream;
        try
        {
            stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (DirectoryNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (asAttachment)
        {
            return File(stream, contentType, downloadName);
        }

        Response.Headers["Content-Disposition"] = $"inline; filename=\"{downloadName}\"";
        return new FileStreamResult(stream, contentType)
        {
            EnableRangeProcessing = false
        };
    }

    private static bool IsGeneratedTenantDocumentFileName(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName != Path.GetFileName(fileName)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var identifier = Path.GetFileNameWithoutExtension(fileName);

        return (extension == ".jpg" || extension == ".png")
            && Guid.TryParseExact(identifier, "N", out _);
    }

    private static string GetTenantDocumentDownloadName(
        int tenantId,
        string documentType,
        string extension)
    {
        var documentLabel = documentType switch
        {
            "IdentityCardFront" => "CCCD-mat-truoc",
            "IdentityCardBack" => "CCCD-mat-sau",
            "BirthCertificate" => "Giay-khai-sinh",
            _ => "Giay-to"
        };

        return $"{documentLabel}-{tenantId}{extension}";
    }


    [HttpGet]
    public async Task<IActionResult> PrintIdentityCards(
        int? tenantId,
        int? roomId,
        int? buildingId)
    {
        Response.Headers["Cache-Control"] = "private, no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        var selectedScopes = (tenantId.HasValue ? 1 : 0)
            + (roomId.HasValue ? 1 : 0)
            + (buildingId.HasValue ? 1 : 0);

        if (selectedScopes > 1)
        {
            return BadRequest();
        }

        var model = new TenantIdentityCardPrintViewModel();

        if (tenantId.HasValue)
        {
            var tenant = await _context.TenantProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.TenantId == tenantId.Value);

            if (tenant is null)
            {
                return NotFound();
            }

            model.Title = $"In CCCD - {tenant.FullName}";
            AddIdentityCardPrintItem(model, tenant.TenantId, tenant.FullName,
                tenant.IdentityCardFrontFileName, tenant.IdentityCardBackFileName, null);

            return View("PrintIdentityCards", model);
        }

        string? scopeRoomLabel = null;

        if (roomId.HasValue)
        {
            var room = await _context.Rooms
                .Include(item => item.Building)
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.RoomId == roomId.Value);

            if (room is null)
            {
                return NotFound();
            }

            scopeRoomLabel = $"{room.Building.BuildingName} - Phòng {room.RoomCode}";
            model.Title = $"In CCCD - {scopeRoomLabel}";
        }
        else if (buildingId.HasValue)
        {
            var building = await _context.Buildings
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.BuildingId == buildingId.Value);

            if (building is null)
            {
                return NotFound();
            }

            model.Title = $"In CCCD - {building.BuildingName}";
        }
        else
        {
            model.Title = "In CCCD - Toàn bộ khu trọ";
        }

        var today = DateTime.Today;
        var assignmentsQuery = _context.ContractTenants
            .AsNoTracking()
            .Where(assignment => assignment.IsActive
                && assignment.Contract.Status == "Active"
                && assignment.Contract.StartDate.Date <= today
                && assignment.Contract.EndDate.Date >= today);

        if (roomId.HasValue)
        {
            assignmentsQuery = assignmentsQuery
                .Where(assignment => assignment.Contract.RoomId == roomId.Value);
        }
        else if (buildingId.HasValue)
        {
            assignmentsQuery = assignmentsQuery
                .Where(assignment => assignment.Contract.Room.BuildingId == buildingId.Value);
        }

        var assignments = await assignmentsQuery
            .OrderBy(assignment => assignment.Tenant.FullName)
            .ThenByDescending(assignment => assignment.Contract.StartDate)
            .Select(assignment => new
            {
                assignment.TenantId,
                assignment.Tenant.FullName,
                assignment.Tenant.IdentityCardFrontFileName,
                assignment.Tenant.IdentityCardBackFileName,
                assignment.Contract.StartDate,
                BuildingName = assignment.Contract.Room.Building.BuildingName,
                assignment.Contract.Room.RoomCode
            })
            .ToListAsync();

        var uniqueTenants = assignments
            .GroupBy(assignment => assignment.TenantId)
            .Select(group => group
                .OrderByDescending(assignment => assignment.StartDate)
                .First())
            .OrderBy(assignment => assignment.FullName)
            .ToList();

        foreach (var assignment in uniqueTenants)
        {
            var roomLabel = $"{assignment.BuildingName} - Phòng {assignment.RoomCode}";
            AddIdentityCardPrintItem(
                model,
                assignment.TenantId,
                assignment.FullName,
                assignment.IdentityCardFrontFileName,
                assignment.IdentityCardBackFileName,
                roomId.HasValue ? scopeRoomLabel : roomLabel);
        }

        return View("PrintIdentityCards", model);
    }

    private void AddIdentityCardPrintItem(
        TenantIdentityCardPrintViewModel model,
        int tenantId,
        string fullName,
        string? frontFileName,
        string? backFileName,
        string? roomLabel)
    {
        var hasFrontImage = TenantDocumentExists(frontFileName);
        var hasBackImage = TenantDocumentExists(backFileName);

        var missingParts = new List<string>();
        if (!hasFrontImage) missingParts.Add("mặt trước");
        if (!hasBackImage) missingParts.Add("mặt sau");

        var item = new TenantIdentityCardPrintItemViewModel
        {
            TenantId = tenantId,
            FullName = fullName,
            RoomLabel = roomLabel,
            MissingDocuments = string.Join(" và ", missingParts)
        };

        if (hasFrontImage && hasBackImage)
        {
            model.PrintableTenants.Add(item);
        }
        else
        {
            model.IncompleteTenants.Add(item);
        }
    }

    private bool TenantDocumentExists(string? fileName)
    {
        if (!IsGeneratedTenantDocumentFileName(fileName))
        {
            return false;
        }

        var filePath = Path.Combine(GetTenantDocumentStorageDirectory(), fileName!);
        return System.IO.File.Exists(filePath);
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

        TryDeleteTenantDocument(tenant.IdentityCardFrontFileName);
        TryDeleteTenantDocument(tenant.IdentityCardBackFileName);
        TryDeleteTenantDocument(tenant.BirthCertificateFileName);

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


    private string GetTenantDocumentStorageDirectory()
    {
        return Path.Combine(_environment.ContentRootPath, "App_Data", "TenantDocuments");
    }

    private static string? NormalizeDocumentType(string? documentType)
    {
        return documentType?.Trim().ToUpperInvariant() switch
        {
            "IDENTITYCARDFRONT" => "IdentityCardFront",
            "IDENTITYCARDBACK" => "IdentityCardBack",
            "BIRTHCERTIFICATE" => "BirthCertificate",
            _ => null
        };
    }

    private static string? GetTenantDocumentFileName(
        TenantProfile tenant,
        string documentType)
    {
        return documentType switch
        {
            "IdentityCardFront" => tenant.IdentityCardFrontFileName,
            "IdentityCardBack" => tenant.IdentityCardBackFileName,
            "BirthCertificate" => tenant.BirthCertificateFileName,
            _ => null
        };
    }

    private static void SetTenantDocumentFileName(
        TenantProfile tenant,
        string documentType,
        string fileName)
    {
        switch (documentType)
        {
            case "IdentityCardFront":
                tenant.IdentityCardFrontFileName = fileName;
                break;
            case "IdentityCardBack":
                tenant.IdentityCardBackFileName = fileName;
                break;
            case "BirthCertificate":
                tenant.BirthCertificateFileName = fileName;
                break;
        }
    }

    private static async Task<string?> ValidateDocumentImageAsync(IFormFile file)
    {
        if (file.Length <= 0 || file.Length > MaxDocumentFileSizeBytes)
        {
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var signature = new byte[8];
        var totalRead = 0;

        await using var stream = file.OpenReadStream();
        while (totalRead < signature.Length)
        {
            var read = await stream.ReadAsync(
                signature.AsMemory(totalRead, signature.Length - totalRead));

            if (read == 0)
            {
                break;
            }

            totalRead += read;
        }

        var isJpeg = totalRead >= 3
            && signature[0] == 0xFF
            && signature[1] == 0xD8
            && signature[2] == 0xFF;

        var isPng = totalRead == 8
            && signature[0] == 0x89
            && signature[1] == 0x50
            && signature[2] == 0x4E
            && signature[3] == 0x47
            && signature[4] == 0x0D
            && signature[5] == 0x0A
            && signature[6] == 0x1A
            && signature[7] == 0x0A;

        if (isJpeg && (extension == ".jpg" || extension == ".jpeg"))
        {
            return ".jpg";
        }

        if (isPng && extension == ".png")
        {
            return ".png";
        }

        return null;
    }

    private void TryDeleteTenantDocument(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName != Path.GetFileName(fileName)
            || fileName.Contains('/')
            || fileName.Contains('\\')
            || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return;
        }

        try
        {
            var filePath = Path.Combine(GetTenantDocumentStorageDirectory(), fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch (IOException)
        {
            // A failed cleanup may leave an orphaned file, but must not undo a saved database change.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed cleanup may leave an orphaned file, but must not undo a saved database change.
        }
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
