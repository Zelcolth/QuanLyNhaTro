using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    public partial class AddInvoicePeriodConfig : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoicePeriodConfigs",
                columns: table => new
                {
                    InvoicePeriodConfigId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BillingMonth = table.Column<DateTime>(type: "date", nullable: false),
                    ElectricityPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GarbageFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WifiFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePeriodConfigs", x => x.InvoicePeriodConfigId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePeriodConfigs_BillingMonth",
                table: "InvoicePeriodConfigs",
                column: "BillingMonth",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoicePeriodConfigs");
        }
    }
}
