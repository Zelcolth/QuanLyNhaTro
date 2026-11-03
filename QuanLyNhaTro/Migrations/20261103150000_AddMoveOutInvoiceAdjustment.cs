using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261103150000_AddMoveOutInvoiceAdjustment")]
    public partial class AddMoveOutInvoiceAdjustment : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "InvoiceAdjustmentAmount",
                table: "MoveOutSettlements",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("InvoiceAdjustmentAmount", "MoveOutSettlements");
        }
    }
}