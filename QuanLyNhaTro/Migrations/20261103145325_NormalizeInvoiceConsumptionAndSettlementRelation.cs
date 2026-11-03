using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeInvoiceConsumptionAndSettlementRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElectricityConsumption",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "WaterConsumption",
                table: "Invoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ElectricityConsumption",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "WaterConsumption",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(@"
    UPDATE [Invoices]
    SET
        [ElectricityConsumption] =
            [CurrentElectricityReading] - [PreviousElectricityReading],
        [WaterConsumption] =
            [CurrentWaterReading] - [PreviousWaterReading];
");
        }
    }
}
