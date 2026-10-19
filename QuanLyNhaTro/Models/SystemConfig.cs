using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models
{
    public class SystemConfig
    {
        [Key]
        public int ConfigId { get; set; }

        public decimal ElectricityPrice { get; set; }

        public decimal WaterPrice { get; set; }

        public decimal GarbageFee { get; set; }

        public decimal WifiFee { get; set; }

        public string? BankCode { get; set; }

        public string? BankAccount { get; set; }

        public string? AccountName { get; set; }
    }
}
