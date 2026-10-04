using Microsoft.EntityFrameworkCore;
using Ryzom.Core.Math3D;
using Ryzom.Core.Persistence;

namespace Ryzom.Tests;

public class PersistenceTests
{
    private RyzomDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<RyzomDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        var db = new RyzomDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    [Fact]
    public async Task CharacterPersistence_StoresAndLoadsInventoryAndCoordinates()
    {
        var db = CreateInMemoryDb(nameof(CharacterPersistence_StoresAndLoadsInventoryAndCoordinates));

        var account = new Account { Username = "MatisNoble", Email = "matis@atys.org" };
        db.Accounts.Add(account);
        await db.SaveChangesAsync();

        var character = new Character
        {
            AccountId = account.Id,
            Name = "SylviaMatis",
            Race = HominRace.Matis,
            Level = 30,
            HitPoints = 1500,
            MaxHitPoints = 1500
        };
        character.SetPosition(new Vector3f(250.5f, 40.2f, 180.75f));

        character.Inventory.Add(new InventoryItem
        {
            Name = "Matis Floral Rapier",
            Slot = EquipmentSlot.MainHand,
            Quality = 150,
            AttackPower = 120
        });

        db.Characters.Add(character);
        await db.SaveChangesAsync();

        var loaded = await db.Characters
            .Include(c => c.Inventory)
            .FirstAsync(c => c.Name == "SylviaMatis");

        Assert.Equal(HominRace.Matis, loaded.Race);
        Assert.Equal(30, loaded.Level);
        Assert.Single(loaded.Inventory);
        Assert.Equal("Matis Floral Rapier", loaded.Inventory[0].Name);
        Assert.Equal(150, loaded.Inventory[0].Quality);

        Assert.True(MathF.Abs(loaded.Position.X - 250.5f) < 1e-3f);
        Assert.True(MathF.Abs(loaded.Position.Y - 40.2f) < 1e-3f);
        Assert.True(MathF.Abs(loaded.Position.Z - 180.75f) < 1e-3f);
    }
}
