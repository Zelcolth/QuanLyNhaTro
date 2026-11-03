using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.ViewModels;

public sealed class MoveOutSettlementViewModel
{
    public int SettlementId { get; set; }
    [Range(1, int.MaxValue)]
    public int ContractId { get; set; }
    public string ContractCode { get; set; } = string.Empty;
    public string RoomLabel { get; set; } = string.Empty;
    public string TenantNames { get; set; } = string.Empty;
    [Required(ErrorMessage = "Vui lòng chọn ngày trả phòng.")]
    public DateTime MoveOutDate { get; set; } = DateTime.Today;
    public decimal DepositAmount { get; set; }
    public decimal OutstandingInvoiceAmount { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số điện trước khi trả phòng không hợp lệ.")]
    public decimal PreviousElectricityReading { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số điện khi trả phòng không hợp lệ.")]
    public decimal FinalElectricityReading { get; set; }
    public decimal ElectricityUnitPrice { get; set; }
    public decimal ElectricityAmount => Math.Max(FinalElectricityReading - PreviousElectricityReading, 0m) * ElectricityUnitPrice;

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số nước trước khi trả phòng không hợp lệ.")]
    public decimal PreviousWaterReading { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số nước khi trả phòng không hợp lệ.")]
    public decimal FinalWaterReading { get; set; }
    public decimal WaterUnitPrice { get; set; }
    public decimal WaterAmount => Math.Max(FinalWaterReading - PreviousWaterReading, 0m) * WaterUnitPrice;

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Phí rác không được âm.")]
    public decimal GarbageFee { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Phí vệ sinh không được âm.")]
    public decimal CleaningFee { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Phí sửa chữa không được âm.")]
    public decimal RepairFee { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Khoản phí khác không được âm.")]
    public decimal OtherFee { get; set; }
    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    public string? Notes { get; set; }
    public string Status { get; set; } = "Draft";
    public bool HasPreviousReading { get; set; }
    public decimal TotalDeductions => OutstandingInvoiceAmount + ElectricityAmount + WaterAmount
        + GarbageFee + CleaningFee + RepairFee + OtherFee;
    public decimal RefundAmount => Math.Max(DepositAmount - TotalDeductions, 0m);
    public decimal AdditionalAmountDue => Math.Max(TotalDeductions - DepositAmount, 0m);
}
