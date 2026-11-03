using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.ViewModels;

public class RentalContractFormViewModel
{
    public int ContractId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phòng.")]
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

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số điện bàn giao không được âm.")]
    [Display(Name = "Chỉ số điện bàn giao")]
    public decimal? MoveInElectricityReading { get; set; }

    [Range(typeof(decimal), "0", "9999999999999999.99", ErrorMessage = "Chỉ số nước bàn giao không được âm.")]
    [Display(Name = "Chỉ số nước bàn giao")]
    public decimal? MoveInWaterReading { get; set; }

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

    [Required(ErrorMessage = "Vui lòng chọn người đứng tên hợp đồng.")]
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn người đứng tên hợp đồng.")]
    [Display(Name = "Người đứng tên hợp đồng")]
    public int? PrimaryTenantId { get; set; }

    public string ContractStatus { get; set; } = "Draft";

    public List<ContractTenantOptionInput> TenantOptions { get; set; } = new();
}

public class ContractTenantOptionInput
{
    public int TenantId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
