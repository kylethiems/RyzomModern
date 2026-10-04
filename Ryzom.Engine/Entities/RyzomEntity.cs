using Ryzom.Core.Math3D;

namespace Ryzom.Engine.Entities;

public enum EntityType : byte
{
    Player = 1,
    Creature = 2,
    NPC = 3,
    HarvestNode = 4
}

public class RyzomEntity
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EntityType Type { get; set; } = EntityType.Player;

    public Vector3f Position { get; set; }
    public Quaternionf Orientation { get; set; } = Quaternionf.Identity;
    public float MovementSpeed { get; set; } = 6.0f; // 6 meters/sec standard run speed

    // Ryzom Classic Tri-Vitals
    public int HitPoints { get; set; } = 1000;
    public int MaxHitPoints { get; set; } = 1000;

    public int Sap { get; set; } = 500; // Magic energy
    public int MaxSap { get; set; } = 500;

    public int Stamina { get; set; } = 800; // Physical energy
    public int MaxStamina { get; set; } = 800;

    public bool IsAlive => HitPoints > 0;

    public void TakeDamage(int amount)
    {
        HitPoints = Math.Max(0, HitPoints - amount);
    }

    public void Heal(int amount)
    {
        HitPoints = Math.Min(MaxHitPoints, HitPoints + amount);
    }

    public bool ConsumeSap(int amount)
    {
        if (Sap < amount) return false;
        Sap -= amount;
        return true;
    }

    public bool ConsumeStamina(int amount)
    {
        if (Stamina < amount) return false;
        Stamina -= amount;
        return true;
    }

    public void Regenerate(float deltaSeconds)
    {
        if (!IsAlive) return;

        // Passive out-of-combat regeneration
        HitPoints = Math.Min(MaxHitPoints, HitPoints + (int)(5 * deltaSeconds));
        Sap = Math.Min(MaxSap, Sap + (int)(10 * deltaSeconds));
        Stamina = Math.Min(MaxStamina, Stamina + (int)(15 * deltaSeconds));
    }
}
