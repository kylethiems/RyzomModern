using Ryzom.Engine.Combat;

namespace Ryzom.Engine.Stanzas;

public enum TargetBrickType
{
    Self = 1,
    SingleTarget = 2,
    AreaOfEffect = 3
}

public enum RangeBrickType
{
    Touch = 1,   // 2 meters
    Short = 2,   // 15 meters
    Medium = 3,  // 25 meters
    Long = 4     // 40 meters
}

public enum SpeedBrickType
{
    Fast = 1,    // 1.0 second
    Normal = 2,  // 1.8 seconds
    Heavy = 3    // 2.8 seconds
}

public class StanzaRecipe
{
    public string Name { get; set; } = "Custom Spell";
    public TargetBrickType Target { get; set; } = TargetBrickType.SingleTarget;
    public DamageType Element { get; set; } = DamageType.Fire;
    public int QualityLevel { get; set; } = 50; // 1 to 250
    public RangeBrickType Range { get; set; } = RangeBrickType.Medium;
    public SpeedBrickType Speed { get; set; } = SpeedBrickType.Normal;
}

public static class StanzaGrammarEngine
{
    public static ActionStanza CompileRecipe(StanzaRecipe recipe)
    {
        if (recipe.QualityLevel < 1 || recipe.QualityLevel > 250)
            throw new ArgumentOutOfRangeException(nameof(recipe.QualityLevel), "Quality must be between 1 and 250.");

        float range = recipe.Range switch
        {
            RangeBrickType.Touch => 2.0f,
            RangeBrickType.Short => 15.0f,
            RangeBrickType.Medium => 25.0f,
            RangeBrickType.Long => 40.0f,
            _ => 20.0f
        };

        float castTime = recipe.Speed switch
        {
            SpeedBrickType.Fast => 1.0f,
            SpeedBrickType.Normal => 1.8f,
            SpeedBrickType.Heavy => 2.8f,
            _ => 1.5f
        };

        // Base damage scales with QualityLevel
        int baseDamage = (int)(recipe.QualityLevel * 3.5f);

        // Sap cost calculation: higher range & AoE increases cost; faster cast increases cost
        int sapCost = (int)(recipe.QualityLevel * 0.45f);
        if (recipe.Target == TargetBrickType.AreaOfEffect)
        {
            sapCost = (int)(sapCost * 1.75f);
            baseDamage = (int)(baseDamage * 0.85f); // AoE deals slightly lower single-target damage
        }

        if (recipe.Range == RangeBrickType.Long) sapCost += 15;
        if (recipe.Speed == SpeedBrickType.Fast) sapCost += 10;

        float cooldown = castTime * 1.25f;

        return new ActionStanza
        {
            Name = recipe.Name,
            SapCost = Math.Max(5, sapCost),
            StaminaCost = 0,
            RangeMeters = range,
            BasePower = baseDamage,
            DamageType = recipe.Element,
            CastTimeSeconds = castTime,
            CooldownSeconds = cooldown
        };
    }
}
