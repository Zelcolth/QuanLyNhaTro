using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    public partial class ContractTenantGroups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ContractTenants",
                columns: table => new
                {
                    ContractId = table.Column<int>(type: "int", nullable: false),
                    TenantId = table.Column<int>(type: "int", nullable: false),
                    IsPrimaryTenant = table.Column<bool>(type: "bit", nullable: false),
                    RelationshipToPrimaryTenant = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractTenants", x => new { x.ContractId, x.TenantId });
                    table.ForeignKey(
                        name: "FK_ContractTenants_RentalContracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "RentalContracts",
                        principalColumn: "ContractId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ContractTenants_TenantProfiles_TenantId",
                        column: x => x.TenantId,
                        principalTable: "TenantProfiles",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Restrict);
                });

            // Preserve existing contracts by converting their current tenant into the primary tenant.
            migrationBuilder.Sql(
                """
                INSERT INTO dbo.ContractTenants
                    (ContractId, TenantId, IsPrimaryTenant, RelationshipToPrimaryTenant)
                SELECT ContractId, TenantId, CAST(1 AS bit), NULL
                FROM dbo.RentalContracts;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_RentalContracts_TenantProfiles_TenantId",
                table: "RentalContracts");

            migrationBuilder.DropIndex(
                name: "IX_RentalContracts_TenantId",
                table: "RentalContracts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RentalContracts");

            migrationBuilder.CreateIndex(
                name: "IX_ContractTenants_TenantId",
                table: "ContractTenants",
                column: "TenantId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "RentalContracts",
                type: "int",
                nullable: true);

            // A legacy contract supports only one tenant, so restore its representative tenant.
            migrationBuilder.Sql(
                """
                UPDATE contract
                SET TenantId = assignment.TenantId
                FROM dbo.RentalContracts AS contract
                CROSS APPLY
                (
                    SELECT TOP (1) TenantId
                    FROM dbo.ContractTenants
                    WHERE ContractId = contract.ContractId
                    ORDER BY IsPrimaryTenant DESC, TenantId ASC
                ) AS assignment;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "TenantId",
                table: "RentalContracts",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RentalContracts_TenantId",
                table: "RentalContracts",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_RentalContracts_TenantProfiles_TenantId",
                table: "RentalContracts",
                column: "TenantId",
                principalTable: "TenantProfiles",
                principalColumn: "TenantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.DropTable(
                name: "ContractTenants");
        }
    }
}
