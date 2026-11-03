using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.ViewModels;

public sealed class RoomDetailsViewModel
{
    public Room Room { get; set; } = null!;
    public RentalContract? ActiveContract { get; set; }
}
