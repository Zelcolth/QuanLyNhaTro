using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.ViewModels;

public class RoomOccupantsViewModel
{
    public int RoomId { get; set; }

    public string RoomLabel { get; set; } = string.Empty;

    public int MaxOccupants { get; set; }

    public int? ContractId { get; set; }

    public string? ContractCode { get; set; }

    public List<ContractTenant> ActiveOccupants { get; set; } = new();

    public List<ContractTenant> FormerOccupants { get; set; } = new();

    public List<TenantProfile> AvailableTenants { get; set; } = new();
}
