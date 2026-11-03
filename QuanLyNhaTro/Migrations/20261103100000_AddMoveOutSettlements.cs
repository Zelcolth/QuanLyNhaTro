using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261103100000_AddMoveOutSettlements")]
    public partial class AddMoveOutSettlements : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MoveOutSettlements",
                columns: table => new
                {
                    SettlementId = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    ContractCodeSnapshot = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RoomLabelSnapshot = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    TenantNamesSnapshot = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    MoveOutDate = table.Column<DateTime>(type: "date", nullable: false),
                    DepositAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingInvoiceAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousElectricityReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalElectricityReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElectricityUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ElectricityAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PreviousWaterReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalWaterReading = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterUnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WaterAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    GarbageFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CleaningFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RepairFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OtherFee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MoveOutSettlements", x => x.SettlementId);
                    table.ForeignKey(
                        name: "FK_MoveOutSettlements_RentalContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "RentalContracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(
                name: "IX_MoveOutSettlements_ContractId",
                table: "MoveOutSettlements",
                column: "ContractId",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "MoveOutSettlements");
        }
    }
}
