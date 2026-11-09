namespace QuanLyNhaTro.ViewModels;

public sealed class TenantIdentityCardPrintViewModel
{
    public string Title { get; set; } = "In CCCD";

    public List<TenantIdentityCardPrintItemViewModel> PrintableTenants { get; set; } = new();

    public List<TenantIdentityCardPrintItemViewModel> IncompleteTenants { get; set; } = new();

    public int TotalTenants => PrintableTenants.Count + IncompleteTenants.Count;
}

public sealed class TenantIdentityCardPrintItemViewModel
{
    public int TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? RoomLabel { get; set; }

    public string MissingDocuments { get; set; } = string.Empty;
}
