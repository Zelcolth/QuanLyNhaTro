namespace QuanLyNhaTro.Models
{
    public class Room
    {
        public int RoomId { get; set; }

        public int BuildingId { get; set; }

        public string RoomCode { get; set; } = string.Empty;


        public decimal MonthlyRent { get; set; }

        public int MaxOccupants { get; set; } = 1;

        public string Status { get; set; } = "Available";

        public string? Description { get; set; }

        public Building Building { get; set; } = null!;
    }
}
