using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyNhaTro.ViewModels;

public class UtilityReadingFormViewModel
{
    public int UtilityReadingId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng.")]
    [Display(Name = "Phòng")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn kỳ ghi chỉ số.")]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Kỳ ghi chỉ số không hợp lệ.")]
    [Display(Name = "Kỳ ghi chỉ số")]
    public string ReadingMonth { get; set; } = DateTime.Today.ToString("yyyy-MM");

    [Required(ErrorMessage = "Vui lòng nhập chỉ số điện mới.")]
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số điện phải là số không âm hợp lệ.")]
    [Display(Name = "Chỉ số điện mới (kWh)")]
    public decimal ElectricityReading { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập chỉ số nước mới.")]
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số nước phải là số không âm hợp lệ.")]
    [Display(Name = "Chỉ số nước mới (m³)")]
    public decimal WaterReading { get; set; }
}

public class UtilityReadingListViewModel
{
    public string ReadingMonthFilter { get; set; } = string.Empty;

    public int? RoomIdFilter { get; set; }

    public List<SelectListItem> RoomOptions { get; set; } = new();

    public List<UtilityReadingListItemViewModel> Readings { get; set; } = new();
}

public class UtilityReadingListItemViewModel
{
    public int UtilityReadingId { get; set; }

    public string BuildingName { get; set; } = string.Empty;

    public string RoomCode { get; set; } = string.Empty;

    public DateTime ReadingMonth { get; set; }

    public DateTime? PreviousReadingMonth { get; set; }

    public decimal? PreviousElectricityReading { get; set; }

    public decimal ElectricityReading { get; set; }

    public decimal? ElectricityConsumption { get; set; }

    public decimal? PreviousWaterReading { get; set; }

    public decimal WaterReading { get; set; }

    public decimal? WaterConsumption { get; set; }

    public DateTime RecordedAt { get; set; }
}


public class UtilityReadingBatchViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn kỳ ghi chỉ số.")]
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Kỳ ghi chỉ số không hợp lệ.")]
    [Display(Name = "Kỳ ghi chỉ số")]
    public string ReadingMonth { get; set; } = DateTime.Today.ToString("yyyy-MM");

    public string CurrentMonth { get; set; } = DateTime.Today.ToString("yyyy-MM");

    public string? StatusMessage { get; set; }

    public List<UtilityReadingBatchRowViewModel> Rooms { get; set; } = new();
}

public class UtilityReadingBatchRowViewModel
{
    public int RoomId { get; set; }

    public string RoomLabel { get; set; } = string.Empty;

    public int? CurrentReadingId { get; set; }

    public DateTime? PreviousReadingMonth { get; set; }

    public decimal? PreviousElectricityReading { get; set; }

    public decimal? PreviousWaterReading { get; set; }

    public decimal? ElectricityReading { get; set; }

    public decimal? WaterReading { get; set; }

    public string? ErrorMessage { get; set; }

    public bool HasCurrentReading { get; set; }

    public bool WasSavedThisRequest { get; set; }
}