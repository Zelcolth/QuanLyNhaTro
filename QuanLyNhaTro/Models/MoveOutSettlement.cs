using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyNhaTro.Models;

public class MoveOutSettlement
{
    [Key]
    public int SettlementId { get; set; }
    public int ContractId { get; set; }
    [Required, StringLength(100)]
    public string ContractCodeSnapshot { get; set; } = string.Empty;
    [Required, StringLength(250)]
    public string RoomLabelSnapshot { get; set; } = string.Empty;
    [Required, StringLength(1000)]
    public string TenantNamesSnapshot { get; set; } = string.Empty;
    public DateTime MoveOutDate { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal OutstandingInvoiceAmount { get; set; }
    public decimal PreviousElectricityReading { get; set; }
    public decimal FinalElectricityReading { get; set; }
    public decimal ElectricityUnitPrice { get; set; }
    public decimal ElectricityAmount { get; set; }
    public decimal PreviousWaterReading { get; set; }
    public decimal FinalWaterReading { get; set; }
    public decimal WaterUnitPrice { get; set; }
    public decimal WaterAmount { get; set; }
    public decimal GarbageFee { get; set; }
    public decimal CleaningFee { get; set; }
    public decimal RepairFee { get; set; }
    public decimal OtherFee { get; set; }
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required, StringLength(20)]
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ConfirmedAt { get; set; }
    public RentalContract Contract { get; set; } = null!;

    [NotMapped]
    public decimal TotalDeductions => OutstandingInvoiceAmount + ElectricityAmount + WaterAmount
        + GarbageFee + CleaningFee + RepairFee + OtherFee;
    [NotMapped]
    public decimal RefundAmount => Math.Max(DepositAmount - TotalDeductions, 0m);
    [NotMapped]
    public decimal AdditionalAmountDue => Math.Max(TotalDeductions - DepositAmount, 0m);
}
