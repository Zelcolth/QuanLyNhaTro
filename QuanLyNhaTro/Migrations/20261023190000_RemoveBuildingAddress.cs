using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    public partial class RemoveBuildingAddress : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Address",
                table: "Buildings");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The removed per-building addresses cannot be reconstructed automatically.
            // Restore the column with an empty value if this migration is rolled back.
            migrationBuilder.AddColumn<string>(
                name: "Address",
                table: "Buildings",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
