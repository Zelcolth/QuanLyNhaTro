using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class InvoicePeriodConfig
{
    [Key]
    public int InvoicePeriodConfigId { get; set; }

    // Always normalized to the first day of the billing month.
    public DateTime BillingMonth { get; set; }

    public decimal ElectricityPrice { get; set; }

    public decimal WaterPrice { get; set; }

    public decimal GarbageFee { get; set; }

    public decimal WifiFee { get; set; }
}
