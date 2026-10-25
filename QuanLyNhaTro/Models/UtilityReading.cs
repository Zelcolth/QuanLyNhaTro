using System.ComponentModel.DataAnnotations;

namespace QuanLyNhaTro.Models;

public class UtilityReading
{
    [Key]
    public int UtilityReadingId { get; set; }

    public int RoomId { get; set; }

    // Normalized to the first day of the recorded month.
    public DateTime ReadingMonth { get; set; }

    public decimal ElectricityReading { get; set; }

    public decimal WaterReading { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.Now;

    public Room Room { get; set; } = null!;
}
