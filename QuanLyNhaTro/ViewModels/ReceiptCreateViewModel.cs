using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.ViewModels;

public sealed class ReceiptCreateViewModel
{
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public string RoomLabel { get; set; } = string.Empty;
    public string TenantNames { get; set; } = string.Empty;
    public DateTime BillingMonth { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn phương thức nhận tiền.")]
    [RegularExpression("^(Cash|BankTransfer)$", ErrorMessage = "Phương thức nhận tiền không hợp lệ.")]
    public string PaymentMethod { get; set; } = "Cash";

    [Required(ErrorMessage = "Vui lòng nhập họ tên người nộp hoặc chuyển tiền.")]
    [StringLength(150, ErrorMessage = "Họ tên không được vượt quá 150 ký tự.")]
    public string PayerName { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ErrorMessage = "Số tiền nhận phải lớn hơn 0.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời điểm nhận tiền.")]
    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    [StringLength(1000, ErrorMessage = "Ghi chú không được vượt quá 1000 ký tự.")]
    public string? Notes { get; set; }
}
