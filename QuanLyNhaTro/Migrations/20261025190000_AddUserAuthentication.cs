using Microsoft.EntityFrameworkCore.Migrations;

namespace QuanLyNhaTro.Migrations;

public partial class AddUserAuthentication : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                UserId = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                FullName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.UserId);
            });

        migrationBuilder.AddColumn<int>(
            name: "UserId",
            table: "TenantProfiles",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_Username",
            table: "Users",
            column: "Username",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TenantProfiles_UserId",
            table: "TenantProfiles",
            column: "UserId",
            unique: true,
            filter: "[UserId] IS NOT NULL");

        migrationBuilder.AddForeignKey(
            name: "FK_TenantProfiles_Users_UserId",
            table: "TenantProfiles",
            column: "UserId",
            principalTable: "Users",
            principalColumn: "UserId",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_TenantProfiles_Users_UserId",
            table: "TenantProfiles");

        migrationBuilder.DropIndex(
            name: "IX_TenantProfiles_UserId",
            table: "TenantProfiles");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "TenantProfiles");

        migrationBuilder.DropTable(name: "Users");
    }
}
