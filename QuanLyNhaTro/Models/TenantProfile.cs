using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class TenantProfile
{
    [Key]
    public int TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? IdentityNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int? BirthYear { get; set; }

    public string? PermanentAddress { get; set; }

    public string? Notes { get; set; }

    public ICollection<ContractTenant> ContractTenants { get; set; }
        = new List<ContractTenant>();
}
