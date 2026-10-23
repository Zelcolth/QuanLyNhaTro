using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    public partial class AllowOptionalTenantPhone : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "TenantProfiles",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the previous NOT NULL constraint safely if optional phones were saved.
            migrationBuilder.Sql(
                "UPDATE dbo.TenantProfiles SET Phone = N'' WHERE Phone IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "TenantProfiles",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);
        }
    }
}
