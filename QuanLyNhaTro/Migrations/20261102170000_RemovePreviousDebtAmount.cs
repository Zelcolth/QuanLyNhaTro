using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261102170000_RemovePreviousDebtAmount")]
    public partial class RemovePreviousDebtAmount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove the old manually-entered debt from existing invoice totals before dropping it.
            migrationBuilder.Sql("""
                UPDATE invoice
                SET
                    [DiscountAmount] = CASE
                        WHEN invoice.[DiscountAmount] > charges.[Subtotal]
                            THEN charges.[Subtotal]
                        ELSE invoice.[DiscountAmount]
                    END,
                    [TotalAmount] = CASE
                        WHEN invoice.[TotalAmount] > invoice.[PreviousDebtAmount]
                            THEN invoice.[TotalAmount] - invoice.[PreviousDebtAmount]
                        ELSE 0
                    END
                FROM [Invoices] AS invoice
                CROSS APPLY (
                    SELECT
                        invoice.[RentAmount]
                        + invoice.[ElectricityAmount]
                        + invoice.[WaterAmount]
                        + invoice.[GarbageFee]
                        + invoice.[WifiFee] AS [Subtotal]
                ) AS charges
                WHERE invoice.[PreviousDebtAmount] > 0;
                """);

            migrationBuilder.DropColumn(
                name: "PreviousDebtAmount",
                table: "Invoices");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Previous manually-entered values cannot be reconstructed after this migration.
            migrationBuilder.AddColumn<decimal>(
                name: "PreviousDebtAmount",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }
    }
}
