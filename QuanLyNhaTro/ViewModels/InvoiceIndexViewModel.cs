namespace QuanLyNhaTro.ViewModels;

public sealed class InvoiceIndexViewModel
{
    public string BillingMonth { get; init; } = string.Empty;
    public string CurrentMonth { get; init; } = string.Empty;
    public string BillingMonthLabel { get; init; } = string.Empty;
    public string PreviousMonthLabel { get; init; } = string.Empty;
    public List<InvoiceItemViewModel> Invoices { get; init; } = new();

    public List<string> RoomOptions => Invoices
        .Select(invoice => invoice.RoomLabel)
        .Where(label => !string.IsNullOrWhiteSpace(label))
        .Distinct()
        .OrderBy(label => label)
        .ToList();

    public int PaidCount => Invoices.Count(invoice => invoice.IsPaid);
    public int UnpaidCount => Invoices.Count(invoice => !invoice.IsPaid);
    public decimal CollectedAmount => Invoices.Where(invoice => invoice.IsPaid).Sum(invoice => invoice.TotalAmount);
    public decimal OutstandingAmount => Invoices.Where(invoice => !invoice.IsPaid).Sum(invoice => invoice.TotalAmount);
    public decimal ElectricityTotal => Invoices.Sum(invoice => invoice.ElectricityAmount);
    public decimal WaterTotal => Invoices.Sum(invoice => invoice.WaterAmount);
    public decimal RevenueTotal => Invoices.Sum(invoice => invoice.TotalAmount);
}
