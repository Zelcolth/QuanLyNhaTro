using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyNhaTro.Models;

public class Invoice
{
    [Key]
    public int InvoiceId { get; set; }

    [Required(ErrorMessage = "Mã hóa đơn không được để trống.")]
    [StringLength(50, ErrorMessage = "Mã hóa đơn không được vượt quá 50 ký tự.")]
    public string InvoiceCode { get; set; } = string.Empty;

    public int ContractId { get; set; }

    // Always normalized to the first day of the billing month.
    public DateTime BillingMonth { get; set; }

    // Actual inclusive occupancy dates covered by this invoice; null on legacy invoices.
    public DateTime? BillingStartDate { get; set; }

    public DateTime? BillingEndDate { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Thông tin phòng của hóa đơn không được để trống.")]
    [StringLength(250, ErrorMessage = "Thông tin phòng không được vượt quá 250 ký tự.")]
    public string RoomLabelSnapshot { get; set; } = string.Empty;

    [Required(ErrorMessage = "Thông tin người thuê của hóa đơn không được để trống.")]
    [StringLength(1000, ErrorMessage = "Thông tin người thuê không được vượt quá 1000 ký tự.")]
    public string TenantNamesSnapshot { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mã hợp đồng của hóa đơn không được để trống.")]
    [StringLength(100, ErrorMessage = "Mã hợp đồng không được vượt quá 100 ký tự.")]
    public string ContractCodeSnapshot { get; set; } = string.Empty;

    public decimal RentAmount { get; set; }

    public decimal PreviousElectricityReading { get; set; }

    public decimal CurrentElectricityReading { get; set; }

    [NotMapped]
    public decimal ElectricityConsumption => CurrentElectricityReading - PreviousElectricityReading;

    public decimal ElectricityUnitPrice { get; set; }

    public decimal ElectricityAmount { get; set; }

    public decimal PreviousWaterReading { get; set; }

    public decimal CurrentWaterReading { get; set; }

    [NotMapped]
    public decimal WaterConsumption => CurrentWaterReading - PreviousWaterReading;

    public decimal WaterUnitPrice { get; set; }

    public decimal WaterAmount { get; set; }

    public decimal GarbageFee { get; set; }

    public decimal WifiFee { get; set; }


    public decimal DiscountAmount { get; set; }

    public decimal TotalAmount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn trạng thái hóa đơn.")]
    [StringLength(20, ErrorMessage = "Trạng thái hóa đơn không được vượt quá 20 ký tự.")]
    public string Status { get; set; } = "Unpaid";

    [StringLength(1000, ErrorMessage = "Ghi chú hóa đơn không được vượt quá 1000 ký tự.")]
    public string? Notes { get; set; }

    public RentalContract Contract { get; set; } = null!;

    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}
