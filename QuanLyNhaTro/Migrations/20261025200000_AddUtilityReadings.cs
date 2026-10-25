using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations;

public partial class AddUtilityReadings : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UtilityReadings",
            columns: table => new
            {
                UtilityReadingId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RoomId = table.Column<int>(type: "int", nullable: false),
                ReadingMonth = table.Column<DateTime>(type: "datetime2", nullable: false),
                ElectricityReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                WaterReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UtilityReadings", x => x.UtilityReadingId);
                table.ForeignKey(
                    name: "FK_UtilityReadings_Rooms_RoomId",
                    column: x => x.RoomId,
                    principalTable: "Rooms",
                    principalColumn: "RoomId",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UtilityReadings_RoomId_ReadingMonth",
            table: "UtilityReadings",
            columns: new[] { "RoomId", "ReadingMonth" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UtilityReadings");
    }
}
