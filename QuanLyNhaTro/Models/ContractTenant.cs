namespace QuanLyNhaTro.Models;

public class ContractTenant
{
    public int ContractId { get; set; }

    public int TenantId { get; set; }

    public bool IsPrimaryTenant { get; set; }

    public string? RelationshipToPrimaryTenant { get; set; }

    public RentalContract Contract { get; set; } = null!;

    public TenantProfile Tenant { get; set; } = null!;
}
