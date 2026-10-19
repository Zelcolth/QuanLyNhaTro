using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyNhaTro.Models;

public class ContractTenant
{
    [Key, Column(Order = 0)]
    public int ContractId { get; set; }

    [Key, Column(Order = 1)]
    public int TenantId { get; set; }

    public bool IsPrimaryTenant { get; set; }

    public string? RelationshipToPrimaryTenant { get; set; }

    public RentalContract Contract { get; set; } = null!;

    public TenantProfile Tenant { get; set; } = null!;
}
