using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261102153000_AddExpectedMoveOutDate")]
    public partial class AddExpectedMoveOutDate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedMoveOutDate",
                table: "RentalContracts",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExpectedMoveOutDate",
                table: "RentalContracts");
        }
    }
}
