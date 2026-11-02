using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261102180000_AddReceiptPaymentDetails")]
    public partial class AddReceiptPaymentDetails : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Receipts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "PayerName",
                table: "Receipts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectorName",
                table: "Receipts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectorName",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "PayerName",
                table: "Receipts");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Receipts");
        }
    }
}
