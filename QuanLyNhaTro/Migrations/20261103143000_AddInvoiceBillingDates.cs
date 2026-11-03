using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261103143000_AddInvoiceBillingDates")]
    public partial class AddInvoiceBillingDates : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "BillingStartDate",
                table: "Invoices",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "BillingEndDate",
                table: "Invoices",
                type: "date",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("BillingStartDate", "Invoices");
            migrationBuilder.DropColumn("BillingEndDate", "Invoices");
        }
    }
}