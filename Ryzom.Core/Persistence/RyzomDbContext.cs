using Microsoft.EntityFrameworkCore;

namespace Ryzom.Core.Persistence;

public class RyzomDbContext : DbContext
{
    public RyzomDbContext(DbContextOptions<RyzomDbContext> options) : base(options) { }

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Character> Characters => Set<Character>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Character>()
            .Property(c => c.PositionX).HasPrecision(12, 4);
        modelBuilder.Entity<Character>()
            .Property(c => c.PositionY).HasPrecision(12, 4);
        modelBuilder.Entity<Character>()
            .Property(c => c.PositionZ).HasPrecision(12, 4);

        // Seed initial test account and character
        modelBuilder.Entity<Account>().HasData(
            new Account { Id = 1, Username = "AtysExplorer", Email = "explorer@atys.org", CreatedAtUtc = DateTime.UtcNow }
        );

        modelBuilder.Entity<Character>().HasData(
            new Character
            {
                Id = 1,
                AccountId = 1,
                Name = "AeronFyros",
                Race = HominRace.Fyros,
                Level = 25,
                HitPoints = 1200,
                MaxHitPoints = 1200,
                Sap = 600,
                MaxSap = 600,
                Stamina = 950,
                MaxStamina = 950,
                PositionX = 120.5000m,
                PositionY = 15.0000m,
                PositionZ = 85.2500m,
                CreatedAtUtc = DateTime.UtcNow
            }
        );

        modelBuilder.Entity<InventoryItem>().HasData(
            new InventoryItem { Id = 1, CharacterId = 1, Name = "Fyros Broadsword", Slot = EquipmentSlot.MainHand, Quantity = 1, Quality = 100, AttackPower = 85 },
            new InventoryItem { Id = 2, CharacterId = 1, Name = "Desert Leather Cuirass", Slot = EquipmentSlot.Chest, Quantity = 1, Quality = 120, ArmorValue = 45 },
            new InventoryItem { Id = 3, CharacterId = 1, Name = "Sap Healing Draught", Slot = EquipmentSlot.Bag, Quantity = 10, Quality = 50 }
        );
    }
}
