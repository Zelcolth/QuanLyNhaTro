using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class RentalContractFormViewModel
{
    public int ContractId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng.")]
    [Display(Name = "Phòng")]
    public int RoomId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã hợp đồng.")]
    [Display(Name = "Mã hợp đồng")]
    public string ContractCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập ngày bắt đầu.")]
    [Display(Name = "Ngày bắt đầu")]
    public DateTime StartDate { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập ngày kết thúc.")]
    [Display(Name = "Ngày kết thúc")]
    public DateTime EndDate { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập giá thuê thỏa thuận.")]
    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ErrorMessage = "Giá thuê thỏa thuận phải lớn hơn 0.")]
    [Display(Name = "Giá thuê thỏa thuận")]
    public decimal MonthlyRent { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiền đặt cọc. Có thể nhập 0.")]
    [Range(typeof(decimal), "0", "9999999999999999.99",
        ErrorMessage = "Tiền đặt cọc không được âm.")]
    [Display(Name = "Tiền đặt cọc")]
    public decimal Deposit { get; set; }

    [Display(Name = "Điều khoản")]
    public string? Terms { get; set; }

    [Required(ErrorMessage = "Hãy chọn một người đại diện trong số những người đã thêm vào hợp đồng.")]
    [Display(Name = "Người đại diện")]
    public int? PrimaryTenantId { get; set; }

    public List<ContractTenantOptionInput> TenantOptions { get; set; } = new();
}

public class ContractTenantOptionInput
{
    public int TenantId { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Phone { get; set; } = string.Empty;

    public bool IsSelected { get; set; }

    [StringLength(100, ErrorMessage = "Quan hệ với người đại diện không được vượt quá 100 ký tự.")]
    [Display(Name = "Quan hệ với người đại diện")]
    public string? RelationshipToPrimaryTenant { get; set; }
}
