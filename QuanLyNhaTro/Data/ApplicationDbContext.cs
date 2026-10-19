using Microsoft.EntityFrameworkCore;
using QuanLyNhaTro.Models;

namespace QuanLyNhaTro.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<TenantProfile> TenantProfiles => Set<TenantProfile>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<ContractTenant> ContractTenants => Set<ContractTenant>();
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Building>()
            .HasMany(building => building.Rooms)
            .WithOne(room => room.Building)
            .HasForeignKey(room => room.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Room>()
            .HasIndex(room => new { room.BuildingId, room.RoomCode })
            .IsUnique();

        modelBuilder.Entity<RentalContract>()
            .HasOne(contract => contract.Room)
            .WithMany()
            .HasForeignKey(contract => contract.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasMany(contract => contract.ContractTenants)
            .WithOne(contractTenant => contractTenant.Contract)
            .HasForeignKey(contractTenant => contractTenant.ContractId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ContractTenant>()
            .HasKey(contractTenant => new { contractTenant.ContractId, contractTenant.TenantId });

        modelBuilder.Entity<ContractTenant>()
            .Property(contractTenant => contractTenant.RelationshipToPrimaryTenant)
            .HasMaxLength(100);

        modelBuilder.Entity<ContractTenant>()
            .HasIndex(contractTenant => contractTenant.TenantId);

        modelBuilder.Entity<TenantProfile>()
            .HasMany(tenant => tenant.ContractTenants)
            .WithOne(contractTenant => contractTenant.Tenant)
            .HasForeignKey(contractTenant => contractTenant.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasIndex(contract => contract.ContractCode)
            .IsUnique();

        modelBuilder.Entity<SystemConfig>(entity =>
        {
            entity.Property(config => config.ElectricityPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(config => config.WaterPrice)
                .HasColumnType("decimal(18,2)");

            entity.Property(config => config.GarbageFee)
                .HasColumnType("decimal(18,2)");

            entity.Property(config => config.WifiFee)
                .HasColumnType("decimal(18,2)");

            entity.Property(config => config.BankCode)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)");

            entity.Property(config => config.BankAccount)
                .HasMaxLength(50)
                .HasColumnType("varchar(50)");

            entity.Property(config => config.AccountName)
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");
        });
    }
}
