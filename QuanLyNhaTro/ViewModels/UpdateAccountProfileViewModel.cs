using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.ViewModels;

public sealed class UpdateAccountProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
    [StringLength(150, ErrorMessage = "Họ và tên không được vượt quá 150 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;
}
