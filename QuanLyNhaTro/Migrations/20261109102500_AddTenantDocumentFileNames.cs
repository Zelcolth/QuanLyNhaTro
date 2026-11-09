using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using QuanLyNhaTro.Data;

#nullable disable

namespace QuanLyNhaTro.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261109102500_AddTenantDocumentFileNames")]
    public partial class AddTenantDocumentFileNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdentityCardFrontFileName",
                table: "TenantProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityCardBackFileName",
                table: "TenantProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BirthCertificateFileName",
                table: "TenantProfiles",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn("IdentityCardFrontFileName", "TenantProfiles");
            migrationBuilder.DropColumn("IdentityCardBackFileName", "TenantProfiles");
            migrationBuilder.DropColumn("BirthCertificateFileName", "TenantProfiles");
        }
    }
}