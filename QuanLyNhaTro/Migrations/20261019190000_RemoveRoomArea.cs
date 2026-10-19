using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261019190000_RemoveRoomArea")]
    public partial class RemoveRoomArea : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Area",
                table: "Rooms");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Area",
                table: "Rooms",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
