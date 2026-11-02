namespace QuanLyNhaTro.Models;

public class ContractTenant
{
    public int ContractId { get; set; }

    public int TenantId { get; set; }

    public bool IsPrimaryTenant { get; set; }

    public string? RelationshipToPrimaryTenant { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime? JoinedAt { get; set; }

    public DateTime? LeftAt { get; set; }

    public RentalContract Contract { get; set; } = null!;

    public TenantProfile Tenant { get; set; } = null!;
}