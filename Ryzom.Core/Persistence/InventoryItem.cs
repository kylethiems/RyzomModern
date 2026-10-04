using System.ComponentModel.DataAnnotations;

namespace Ryzom.Core.Persistence;

public enum EquipmentSlot
{
    Bag = 0,
    Head = 1,
    Chest = 2,
    Hands = 3,
    Legs = 4,
    Feet = 5,
    MainHand = 6,
    OffHand = 7
}

public class InventoryItem
{
    public int Id { get; set; }

    public int CharacterId { get; set; }
    public Character? Character { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    public EquipmentSlot Slot { get; set; } = EquipmentSlot.Bag;

    public int Quantity { get; set; } = 1;

    // Ryzom item quality scale (1 to 250)
    public int Quality { get; set; } = 50;

    public int ArmorValue { get; set; } = 0;
    public int AttackPower { get; set; } = 0;
}
