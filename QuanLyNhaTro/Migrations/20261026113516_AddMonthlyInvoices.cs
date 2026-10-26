using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    /// <inheritdoc />
    public partial class AddMonthlyInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Invoices",
                columns: table => new
                {
                    InvoiceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    BillingMonth = table.Column<DateTime>(type: "date", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RoomLabelSnapshot = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TenantNamesSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ContractCodeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RentAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousElectricityReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentElectricityReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElectricityConsumption = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElectricityUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElectricityAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousWaterReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CurrentWaterReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterConsumption = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GarbageFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WifiFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invoices", x => x.InvoiceId);
                    table.ForeignKey(
                        name: "FK_Invoices_RentalContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "RentalContracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_BillingMonth",
                table: "Invoices",
                column: "BillingMonth");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_ContractId_BillingMonth",
                table: "Invoices",
                columns: new[] { "ContractId", "BillingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_InvoiceCode",
                table: "Invoices",
                column: "InvoiceCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Invoices");
        }
    }
}
