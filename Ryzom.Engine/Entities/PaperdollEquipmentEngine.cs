using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryzom.Engine.Entities;

public enum EquipmentSlot
{
    Head = 0,
    Chest = 1,
    Arms = 2,
    Hands = 3,
    Legs = 4,
    Feet = 5,
    RightHand = 6,
    LeftHand = 7,
    TwoHanded = 8,
    Earring1 = 9,
    Earring2 = 10,
    Ring1 = 11,
    Ring2 = 12,
    Anklet = 13,
    Pendant = 14
}

public enum MaterialQualityTier
{
    BranchwoodQ20 = 20,
    SapwoodQ50 = 50,
    SeasonedHeartwoodQ100 = 100,
    PrimeRootsAmberQ200 = 200,
    KamiBlessedQ250 = 250
}

/// <summary>
/// An equipped gear item in the Ryzom 14-slot paperdoll system.
/// </summary>
public record GearItem(
    string ItemId,
    string ItemName,
    EquipmentSlot Slot,
    MaterialQualityTier Quality,
    int ArmorRating,
    int ElementalResist,
    float WeightKg,
    string VisualMeshId
);

/// <summary>
/// Combined defensive and combat stats derived from paperdoll gear.
/// </summary>
public record PaperdollStats(
    int TotalArmorRating,
    int TotalElementalResist,
    float TotalWeightKg,
    float EncumbrancePenaltyPercent,
    int EquippedSlotCount
);

/// <summary>
/// Engine managing 14-slot paperdoll equipment, item equipping/unequipping,
/// stat derivation, and visual mesh attachment bindings.
/// </summary>
public class PaperdollEquipmentEngine
{
    private readonly Dictionary<EquipmentSlot, GearItem> _equippedItems = new();

    public int EquippedCount => _equippedItems.Count;

    public bool Equip(GearItem item, out GearItem? unequippedPrevious)
    {
        // Two-handed weapon logic: replaces both RightHand and LeftHand
        if (item.Slot == EquipmentSlot.TwoHanded)
        {
            _equippedItems.Remove(EquipmentSlot.RightHand);
            _equippedItems.Remove(EquipmentSlot.LeftHand);
        }
        else if (item.Slot == EquipmentSlot.RightHand || item.Slot == EquipmentSlot.LeftHand)
        {
            _equippedItems.Remove(EquipmentSlot.TwoHanded);
        }

        _equippedItems.TryGetValue(item.Slot, out unequippedPrevious);
        _equippedItems[item.Slot] = item;
        return true;
    }

    public bool Unequip(EquipmentSlot slot, out GearItem? unequipped)
    {
        return _equippedItems.Remove(slot, out unequipped);
    }

    public GearItem? GetEquipped(EquipmentSlot slot)
    {
        return _equippedItems.TryGetValue(slot, out var item) ? item : null;
    }

    public PaperdollStats CalculateTotalStats()
    {
        int totalArmor = _equippedItems.Values.Sum(i => i.ArmorRating);
        int totalResist = _equippedItems.Values.Sum(i => i.ElementalResist);
        float totalWeight = _equippedItems.Values.Sum(i => i.WeightKg);

        // Encumbrance penalty begins above 15kg
        float penalty = totalWeight > 15f ? Math.Min(50f, (totalWeight - 15f) * 1.5f) : 0f;

        return new PaperdollStats(
            TotalArmorRating: totalArmor,
            TotalElementalResist: totalResist,
            TotalWeightKg: totalWeight,
            EncumbrancePenaltyPercent: penalty,
            EquippedSlotCount: _equippedItems.Count
        );
    }

    public IEnumerable<string> GetActiveVisualMeshIds()
    {
        return _equippedItems.Values.Select(i => i.VisualMeshId).Where(id => !string.IsNullOrEmpty(id));
    }
}
