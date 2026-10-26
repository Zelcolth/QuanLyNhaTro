using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.ViewModels;

public sealed class InvoiceItemViewModel
{
    public int InvoiceId { get; init; }
    public int ContractId { get; init; }
    public string InvoiceCode { get; init; } = string.Empty;
    public string RoomLabel { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string Status { get; init; } = "Unpaid";
    public string ContractStatus { get; init; } = string.Empty;

    public bool IsPaid => Status == "Paid";

    public string ContractStatusLabel => ContractStatus switch
    {
        "Active" => "Đang hiệu lực",
        "Expired" => "Hết hạn",
        "Terminated" => "Đã chấm dứt",
        "Completed" => "Đã hoàn thành",
        "Draft" => "Bản nháp",
        "Cancelled" => "Đã hủy",
        _ => ContractStatus
    };

    public decimal RentAmount { get; init; }
    public decimal PreviousElectricityReading { get; init; }
    public decimal CurrentElectricityReading { get; init; }
    public decimal ElectricityConsumption { get; init; }
    public decimal ElectricityUnitPrice { get; init; }
    public decimal ElectricityAmount { get; init; }
    public decimal PreviousWaterReading { get; init; }
    public decimal CurrentWaterReading { get; init; }
    public decimal WaterConsumption { get; init; }
    public decimal WaterUnitPrice { get; init; }
    public decimal WaterAmount { get; init; }
    public decimal GarbageFee { get; init; }
    public decimal WifiFee { get; init; }
    public decimal PreviousDebtAmount { get; init; }
    public decimal DiscountAmount { get; init; }
    public decimal TotalAmount { get; init; }

    public static InvoiceItemViewModel FromInvoice(Invoice invoice)
    {
        return new InvoiceItemViewModel
        {
            InvoiceId = invoice.InvoiceId,
            ContractId = invoice.ContractId,
            InvoiceCode = invoice.InvoiceCode,
            RoomLabel = invoice.RoomLabelSnapshot,
            TenantName = invoice.TenantNamesSnapshot
                .Split(", ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault() ?? invoice.TenantNamesSnapshot,
            Status = invoice.Status,
            ContractStatus = invoice.Contract?.Status ?? string.Empty,
            RentAmount = invoice.RentAmount,
            PreviousElectricityReading = invoice.PreviousElectricityReading,
            CurrentElectricityReading = invoice.CurrentElectricityReading,
            ElectricityConsumption = invoice.ElectricityConsumption,
            ElectricityUnitPrice = invoice.ElectricityUnitPrice,
            ElectricityAmount = invoice.ElectricityAmount,
            PreviousWaterReading = invoice.PreviousWaterReading,
            CurrentWaterReading = invoice.CurrentWaterReading,
            WaterConsumption = invoice.WaterConsumption,
            WaterUnitPrice = invoice.WaterUnitPrice,
            WaterAmount = invoice.WaterAmount,
            GarbageFee = invoice.GarbageFee,
            WifiFee = invoice.WifiFee,
            PreviousDebtAmount = invoice.PreviousDebtAmount,
            DiscountAmount = invoice.DiscountAmount,
            TotalAmount = invoice.TotalAmount
        };
    }
}
