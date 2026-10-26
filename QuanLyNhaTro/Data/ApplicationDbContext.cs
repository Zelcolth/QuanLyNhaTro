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
    public DbSet<UtilityReading> UtilityReadings => Set<UtilityReading>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<User> Users => Set<User>();

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

        modelBuilder.Entity<User>()
            .HasIndex(user => user.Username)
            .IsUnique();

        modelBuilder.Entity<TenantProfile>()
            .HasOne(tenant => tenant.User)
            .WithOne()
            .HasForeignKey<TenantProfile>(tenant => tenant.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TenantProfile>()
            .HasIndex(tenant => tenant.UserId)
            .IsUnique()
            .HasFilter("[UserId] IS NOT NULL");

        modelBuilder.Entity<TenantProfile>()
            .HasMany(tenant => tenant.ContractTenants)
            .WithOne(contractTenant => contractTenant.Tenant)
            .HasForeignKey(contractTenant => contractTenant.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasIndex(contract => contract.ContractCode)
            .IsUnique();

        modelBuilder.Entity<UtilityReading>(entity =>
        {
            entity.Property(reading => reading.ElectricityReading)
                .HasColumnType("decimal(18,2)");

            entity.Property(reading => reading.WaterReading)
                .HasColumnType("decimal(18,2)");

            entity.HasIndex(reading => new { reading.RoomId, reading.ReadingMonth })
                .IsUnique();

            entity.HasOne(reading => reading.Room)
                .WithMany()
                .HasForeignKey(reading => reading.RoomId)
                .OnDelete(DeleteBehavior.Restrict);
        });

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

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(invoice => invoice.BillingMonth)
                .HasColumnType("date");

            entity.Property(invoice => invoice.InvoiceCode)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(invoice => invoice.RoomLabelSnapshot)
                .HasMaxLength(250)
                .IsRequired();

            entity.Property(invoice => invoice.TenantNamesSnapshot)
                .HasMaxLength(1000)
                .IsRequired();

            entity.Property(invoice => invoice.ContractCodeSnapshot)
                .HasMaxLength(100)
                .IsRequired();

            entity.Property(invoice => invoice.Status)
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(invoice => invoice.Notes)
                .HasMaxLength(1000);

            entity.Property(invoice => invoice.RentAmount).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.PreviousElectricityReading).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.CurrentElectricityReading).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.ElectricityConsumption).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.ElectricityUnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.ElectricityAmount).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.PreviousWaterReading).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.CurrentWaterReading).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.WaterConsumption).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.WaterUnitPrice).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.WaterAmount).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.GarbageFee).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.WifiFee).HasColumnType("decimal(18,2)");
            entity.Property(invoice => invoice.TotalAmount).HasColumnType("decimal(18,2)");

            entity.HasIndex(invoice => invoice.InvoiceCode)
                .IsUnique();

            entity.HasIndex(invoice => new { invoice.ContractId, invoice.BillingMonth })
                .IsUnique();

            entity.HasIndex(invoice => invoice.BillingMonth);

            entity.HasOne(invoice => invoice.Contract)
                .WithMany()
                .HasForeignKey(invoice => invoice.ContractId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
