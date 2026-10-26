using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class Invoice
{
    [Key]
    public int InvoiceId { get; set; }

    [Required, StringLength(50)]
    public string InvoiceCode { get; set; } = string.Empty;

    public int ContractId { get; set; }

    // Always normalized to the first day of the billing month.
    public DateTime BillingMonth { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.Now;

    [Required, StringLength(250)]
    public string RoomLabelSnapshot { get; set; } = string.Empty;

    [Required, StringLength(1000)]
    public string TenantNamesSnapshot { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string ContractCodeSnapshot { get; set; } = string.Empty;

    public decimal RentAmount { get; set; }

    public decimal PreviousElectricityReading { get; set; }

    public decimal CurrentElectricityReading { get; set; }

    public decimal ElectricityConsumption { get; set; }

    public decimal ElectricityUnitPrice { get; set; }

    public decimal ElectricityAmount { get; set; }

    public decimal PreviousWaterReading { get; set; }

    public decimal CurrentWaterReading { get; set; }

    public decimal WaterConsumption { get; set; }

    public decimal WaterUnitPrice { get; set; }

    public decimal WaterAmount { get; set; }

    public decimal GarbageFee { get; set; }

    public decimal WifiFee { get; set; }

    public decimal PreviousDebtAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Unpaid";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public RentalContract Contract { get; set; } = null!;
}
