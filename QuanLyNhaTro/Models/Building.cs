using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models
{
    public class Building
    {
        public int BuildingId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên tòa nhà.")]
        [Display(Name = "Tên tòa nhà")]
        public string BuildingName { get; set; } = string.Empty;

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
