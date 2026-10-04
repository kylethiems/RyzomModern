using System;
using System.Collections.Generic;
using System.Linq;

namespace Ryzom.Engine.Stanza;

public enum BrickCategory
{
    Target = 0,
    Effect = 1,
    Modifier = 2,
    Cost = 3
}

public enum ElementalDomain
{
    None = 0,
    Fire = 1,
    Cold = 2,
    Electric = 3,
    Acid = 4,
    Rot = 5,
    Shock = 6,
    Poison = 7,
    Life = 8
}

/// <summary>
/// Atomic building block (Brick) in the Ryzom Action Stanza grammar.
/// </summary>
public record StanzaBrick(
    string BrickId,
    string Name,
    BrickCategory Category,
    ElementalDomain Domain,
    int PowerScore,         // Points required (for effects) or provided (for costs)
    int SapCost,
    int StaminaCost,
    int HpCost,
    float RangeMeters,
    float CastTimeSeconds
);

/// <summary>
/// Assembled and validated Stanza action ready for execution.
/// </summary>
public record CompiledStanza(
    string StanzaName,
    bool IsValid,
    string? ValidationError,
    StanzaBrick TargetBrick,
    List<StanzaBrick> EffectBricks,
    List<StanzaBrick> ModifierBricks,
    List<StanzaBrick> CostBricks,
    int TotalSapCost,
    int TotalStaminaCost,
    int TotalHpCost,
    float FinalRangeMeters,
    float FinalCastTimeSeconds,
    int TotalPowerRating
);

/// <summary>
/// Compiler and validator for Ryzom Action Stanzas, enforcing credit balancing rules:
/// Sum(Cost Bricks) >= Sum(Effect Bricks * Modifiers).
/// </summary>
public class StanzaActionGrammarCompiler
{
    private readonly Dictionary<string, StanzaBrick> _brickCatalog = new();

    public StanzaActionGrammarCompiler()
    {
        RegisterStandardBricks();
    }

    public int CatalogSize => _brickCatalog.Count;

    public void RegisterBrick(StanzaBrick brick)
    {
        _brickCatalog[brick.BrickId] = brick;
    }

    public StanzaBrick? GetBrick(string brickId)
    {
        return _brickCatalog.TryGetValue(brickId, out var b) ? b : null;
    }

    /// <summary>
    /// Compiles a set of brick IDs into a validated, executable Stanza.
    /// </summary>
    public CompiledStanza Compile(string stanzaName, IEnumerable<string> brickIds)
    {
        var bricks = brickIds.Select(id => GetBrick(id)).Where(b => b != null).Select(b => b!).ToList();

        var target = bricks.FirstOrDefault(b => b.Category == BrickCategory.Target);
        if (target == null)
        {
            return new CompiledStanza(stanzaName, false, "Missing Target brick (e.g. Self or Target)",
                null!, new(), new(), new(), 0, 0, 0, 0f, 0f, 0);
        }

        var effects = bricks.Where(b => b.Category == BrickCategory.Effect).ToList();
        if (effects.Count == 0)
        {
            return new CompiledStanza(stanzaName, false, "Missing Effect brick (e.g. Fire Damage or Heal)",
                target, new(), new(), new(), 0, 0, 0, 0f, 0f, 0);
        }

        var modifiers = bricks.Where(b => b.Category == BrickCategory.Modifier).ToList();
        var costs = bricks.Where(b => b.Category == BrickCategory.Cost).ToList();

        // Compute modifiers
        float rangeBoost = 1.0f + modifiers.Sum(m => m.RangeMeters > 0 ? (m.RangeMeters / 10f) : 0f);
        float castSpeedMult = Math.Max(0.4f, 1.0f - modifiers.Sum(m => m.CastTimeSeconds < 0 ? MathF.Abs(m.CastTimeSeconds) : 0f));
        float powerMult = 1.0f + modifiers.Sum(m => m.PowerScore > 0 ? (m.PowerScore * 0.15f) : 0f);

        int rawEffectPower = (int)MathF.Round(effects.Sum(e => e.PowerScore) * powerMult);
        int totalCostCredit = costs.Sum(c => c.PowerScore);

        // Validation rule: Cost credit must balance or exceed effect power
        if (totalCostCredit < rawEffectPower)
        {
            return new CompiledStanza(
                stanzaName,
                false,
                $"Insufficient Cost Credit: Requires {rawEffectPower} pts, but provided Cost bricks only offer {totalCostCredit} pts",
                target, effects, modifiers, costs, 0, 0, 0, 0f, 0f, rawEffectPower
            );
        }

        int finalSap = costs.Sum(c => c.SapCost) + effects.Sum(e => e.SapCost);
        int finalStamina = costs.Sum(c => c.StaminaCost) + effects.Sum(e => e.StaminaCost);
        int finalHp = costs.Sum(c => c.HpCost) + effects.Sum(e => e.HpCost);

        float finalRange = target.RangeMeters * rangeBoost;
        float baseCast = Math.Max(0.5f, effects.Max(e => e.CastTimeSeconds));
        float finalCastTime = baseCast * castSpeedMult;

        return new CompiledStanza(
            StanzaName: stanzaName,
            IsValid: true,
            ValidationError: null,
            TargetBrick: target,
            EffectBricks: effects,
            ModifierBricks: modifiers,
            CostBricks: costs,
            TotalSapCost: finalSap,
            TotalStaminaCost: finalStamina,
            TotalHpCost: finalHp,
            FinalRangeMeters: finalRange,
            FinalCastTimeSeconds: finalCastTime,
            TotalPowerRating: rawEffectPower
        );
    }

    private void RegisterStandardBricks()
    {
        // Targets
        RegisterBrick(new StanzaBrick("target_self", "Self", BrickCategory.Target, ElementalDomain.None, 0, 0, 0, 0, 0f, 0f));
        RegisterBrick(new StanzaBrick("target_single", "Target", BrickCategory.Target, ElementalDomain.None, 0, 0, 0, 0, 20f, 0f));
        RegisterBrick(new StanzaBrick("target_aoe", "Area of Effect (Sphere)", BrickCategory.Target, ElementalDomain.None, 5, 0, 0, 0, 15f, 0.5f));

        // Effects
        RegisterBrick(new StanzaBrick("effect_fire_direct", "Fire Damage I", BrickCategory.Effect, ElementalDomain.Fire, 10, 0, 0, 0, 0f, 1.8f));
        RegisterBrick(new StanzaBrick("effect_cold_direct", "Cold Damage I", BrickCategory.Effect, ElementalDomain.Cold, 10, 0, 0, 0, 0f, 1.8f));
        RegisterBrick(new StanzaBrick("effect_shock_direct", "Shock Damage I", BrickCategory.Effect, ElementalDomain.Shock, 12, 0, 0, 0, 0f, 1.5f));
        RegisterBrick(new StanzaBrick("effect_heal_life", "Life Heal I", BrickCategory.Effect, ElementalDomain.Life, 10, 0, 0, 0, 0f, 2.0f));
        RegisterBrick(new StanzaBrick("effect_blindness", "Ocular Blindness", BrickCategory.Effect, ElementalDomain.None, 15, 0, 0, 0, 0f, 2.2f));

        // Modifiers
        RegisterBrick(new StanzaBrick("mod_range_boost", "Range Booster (+5m)", BrickCategory.Modifier, ElementalDomain.None, 0, 0, 0, 0, 5f, 0f));
        RegisterBrick(new StanzaBrick("mod_quick_cast", "Fast Cast (-20%)", BrickCategory.Modifier, ElementalDomain.None, 0, 0, 0, 0, 0f, -0.2f));
        RegisterBrick(new StanzaBrick("mod_power_mult", "Potency Amplification (+15%)", BrickCategory.Modifier, ElementalDomain.None, 1, 0, 0, 0, 0f, 0f));

        // Costs
        RegisterBrick(new StanzaBrick("cost_sap_20", "Sap Credit 20", BrickCategory.Cost, ElementalDomain.None, 10, 20, 0, 0, 0f, 0f));
        RegisterBrick(new StanzaBrick("cost_sap_40", "Sap Credit 40", BrickCategory.Cost, ElementalDomain.None, 20, 40, 0, 0, 0f, 0f));
        RegisterBrick(new StanzaBrick("cost_hp_sacrifice", "Blood Sac (25 HP)", BrickCategory.Cost, ElementalDomain.None, 15, 0, 0, 25, 0f, 0f));
        RegisterBrick(new StanzaBrick("cost_stamina_30", "Stamina Burn 30", BrickCategory.Cost, ElementalDomain.None, 10, 0, 30, 0, 0f, 0f));
    }
}
