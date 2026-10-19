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
    public DbSet<SystemConfig> SystemConfigs => Set<SystemConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Building>()
            .HasMany(b => b.Rooms)
            .WithOne(r => r.Building)
            .HasForeignKey(r => r.BuildingId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Room>()
            .HasIndex(r => new { r.BuildingId, r.RoomCode })
            .IsUnique();

        modelBuilder.Entity<RentalContract>()
            .HasOne(c => c.Room)
            .WithMany()
            .HasForeignKey(c => c.RoomId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasOne(c => c.Tenant)
            .WithMany(t => t.Contracts)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RentalContract>()
            .HasIndex(c => c.ContractCode)
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
