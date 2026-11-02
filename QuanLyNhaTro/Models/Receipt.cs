using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class Receipt
{
    [Key]
    public int ReceiptId { get; set; }

    [Required]
    [StringLength(50)]
    public string ReceiptCode { get; set; } = string.Empty;

    public int InvoiceId { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.Now;

    [Range(typeof(decimal), "0.01", "9999999999999999.99",
        ErrorMessage = "Số tiền thu phải lớn hơn 0.")]
    public decimal Amount { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
