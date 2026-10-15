using System.ComponentModel.DataAnnotations;
namespace QuanLyNhaTro.Models
{
    public class RentalContract
    {
        [Key]
        public int ContractId { get; set; }

        public int RoomId { get; set; }

        public int TenantId { get; set; }

        public string ContractCode { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public decimal MonthlyRent { get; set; }

        public decimal Deposit { get; set; }

        public string Status { get; set; } = "Draft";

        public string? Terms { get; set; }

        public Room Room { get; set; } = null!;

        public TenantProfile Tenant { get; set; } = null!;
    }
}
