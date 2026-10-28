using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models
{
    public class Room
    {
        public int RoomId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tòa nhà.")]
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tòa nhà.")]
        [Display(Name = "Tòa nhà")]
        public int BuildingId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã phòng.")]
        [Display(Name = "Mã phòng")]
        public string RoomCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập giá thuê niêm yết.")]
        [Range(typeof(decimal), "0.01", "9999999999999999.99",
            ErrorMessage = "Giá thuê niêm yết phải lớn hơn 0.")]
        [Display(Name = "Giá thuê niêm yết")]
        public decimal MonthlyRent { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Số người tối đa phải ít nhất là 1.")]
        [Display(Name = "Số người tối đa")]
        public int MaxOccupants { get; set; } = 1;

        [Required(ErrorMessage = "Vui lòng chọn trạng thái phòng.")]
        [Display(Name = "Trạng thái phòng")]
        public string Status { get; set; } = "Available";

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        public Building Building { get; set; } = null!;
    }
}
