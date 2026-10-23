using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class TenantProfile
{
    [Key]
    public int TenantId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    public string FullName { get; set; } = string.Empty;

    public string? Phone { get; set; }

    [RegularExpression(
    @"^(?:[0-9]{9}|[0-9]{12})$",
    ErrorMessage = "Số CCCD/CMND phải gồm đúng 9 hoặc 12 chữ số.")]
    public string? IdentityNumber { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public int? BirthYear { get; set; }

    public string? PermanentAddress { get; set; }

    public string? Notes { get; set; }

    public ICollection<ContractTenant> ContractTenants { get; set; }
        = new List<ContractTenant>();
}
