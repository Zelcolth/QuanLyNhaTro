using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    public partial class RemoveReservedRoomStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE room
                SET Status = CASE
                    WHEN EXISTS
                    (
                        SELECT 1
                        FROM dbo.RentalContracts AS contract
                        WHERE contract.RoomId = room.RoomId
                          AND contract.Status = N'Active'
                          AND CAST(contract.StartDate AS date) <= CAST(GETDATE() AS date)
                          AND CAST(contract.EndDate AS date) >= CAST(GETDATE() AS date)
                    )
                    THEN N'Occupied'
                    ELSE N'Available'
                END
                FROM dbo.Rooms AS room
                WHERE room.Status = N'Reserved';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no reverse data update: there is no reliable way to tell
            // which Available/Occupied rooms were previously marked Reserved.
        }
    }
}
