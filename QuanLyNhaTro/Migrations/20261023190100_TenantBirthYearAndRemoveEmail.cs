using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    public partial class TenantBirthYearAndRemoveEmail : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BirthYear",
                table: "TenantProfiles",
                type: "int",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "Email",
                table: "TenantProfiles");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "TenantProfiles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "BirthYear",
                table: "TenantProfiles");
        }
    }
}
