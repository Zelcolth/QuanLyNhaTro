namespace QuanLyNhaTro.Models
{
    public class Building
    {
        public int BuildingId { get; set; }

        public string BuildingName { get; set; } = string.Empty;

        public string Address { get; set; } = string.Empty;

        public string? Description { get; set; }

        public ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
