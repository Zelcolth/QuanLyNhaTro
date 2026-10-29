using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.ViewModels;

public class SystemConfigViewModel : IValidatableObject
{
    [Required(ErrorMessage = "Vui lòng nhập đơn giá điện.")]
    [Range(typeof(decimal), "0.01", "10000000", ErrorMessage = "Đơn giá điện phải lớn hơn 0.")]
    [Display(Name = "Đơn giá điện (VNĐ/kWh)")]
    public decimal ElectricityPrice { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập đơn giá nước.")]
    [Range(typeof(decimal), "0.01", "10000000", ErrorMessage = "Đơn giá nước phải lớn hơn 0.")]
    [Display(Name = "Đơn giá nước (VNĐ/m³)")]
    public decimal WaterPrice { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập phí rác; có thể nhập 0.")]
    [Range(typeof(decimal), "0", "10000000", ErrorMessage = "Phí rác không được âm.")]
    [Display(Name = "Phí rác hàng tháng (VNĐ)")]
    public decimal GarbageFee { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập phí Wi-Fi; có thể nhập 0.")]
    [Range(typeof(decimal), "0", "10000000", ErrorMessage = "Phí Wi-Fi không được âm.")]
    [Display(Name = "Phí Wi-Fi hàng tháng (VNĐ)")]
    public decimal WifiFee { get; set; }

    [StringLength(50, ErrorMessage = "Mã ngân hàng không được vượt quá 50 ký tự.")]
    [Display(Name = "Mã ngân hàng")]
    public string? BankCode { get; set; }

    [StringLength(50, ErrorMessage = "Số tài khoản không được vượt quá 50 ký tự.")]
    [Display(Name = "Số tài khoản nhận tiền")]
    public string? BankAccount { get; set; }

    [StringLength(100, ErrorMessage = "Tên chủ tài khoản không được vượt quá 100 ký tự.")]
    [Display(Name = "Tên chủ tài khoản")]
    public string? AccountName { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasAnyBankDetail =
            !string.IsNullOrWhiteSpace(BankCode)
            || !string.IsNullOrWhiteSpace(BankAccount)
            || !string.IsNullOrWhiteSpace(AccountName);

        if (!hasAnyBankDetail)
        {
            yield break;
        }

        if (string.IsNullOrWhiteSpace(BankCode))
        {
            yield return new ValidationResult(
                "Vui lòng nhập mã ngân hàng khi cấu hình thông tin nhận tiền.",
                new[] { nameof(BankCode) });
        }

        if (string.IsNullOrWhiteSpace(BankAccount))
        {
            yield return new ValidationResult(
                "Vui lòng nhập số tài khoản khi cấu hình thông tin nhận tiền.",
                new[] { nameof(BankAccount) });
        }

        if (string.IsNullOrWhiteSpace(AccountName))
        {
            yield return new ValidationResult(
                "Vui lòng nhập tên chủ tài khoản khi cấu hình thông tin nhận tiền.",
                new[] { nameof(AccountName) });
        }
    }
}
