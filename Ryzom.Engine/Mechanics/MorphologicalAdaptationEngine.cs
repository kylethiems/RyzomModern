using Ryzom.Core.Math3D;

namespace Ryzom.Engine.Mechanics;

public enum MagicElement
{
    None,
    Fire,
    Cold,
    Shock,
    Acid,
    RotSap
}

public record MorphologicalState(
    float DominantArmScale,
    float ShoulderBreadthScale,
    float OcularGlowIntensity,
    Vector3f OcularColor,
    float DermalVeinGlow,
    int SpellEvolutionTier,
    float CastSpeedBonus,
    string VisualDescription
);

public class MorphologicalAdaptationEngine
{
    private int _meleeSwingCount;
    private readonly Dictionary<MagicElement, int> _elementalCastCounts = new()
    {
        [MagicElement.Fire] = 0,
        [MagicElement.Cold] = 0,
        [MagicElement.Shock] = 0,
        [MagicElement.Acid] = 0,
        [MagicElement.RotSap] = 0
    };

    private float _recentMagicCharge;
    private MagicElement _dominantElement = MagicElement.None;

    public int MeleeSwingCount => _meleeSwingCount;
    public IReadOnlyDictionary<MagicElement, int> ElementalCastCounts => _elementalCastCounts;

    public void RecordMeleeSwing(int count = 1)
    {
        _meleeSwingCount += Math.Max(1, count);
    }

    public void RecordSpellCast(MagicElement element, int count = 1)
    {
        if (element == MagicElement.None) return;

        _elementalCastCounts[element] += Math.Max(1, count);
        _recentMagicCharge = Math.Clamp(_recentMagicCharge + (0.15f * count), 0f, 1.0f);
        _dominantElement = element;
    }

    public void TickMetabolicDecay(float elapsedHours)
    {
        // Magic charge cools down toward baseline
        float coolRate = 0.25f * elapsedHours;
        _recentMagicCharge = Math.Max(0f, _recentMagicCharge - coolRate);
    }

    public MorphologicalState EvaluateMorphology()
    {
        // 1. Musculoskeletal Hypertrophy: Allometric logarithmic growth curve
        // Arm scale caps at 1.35x (+35% volume), shoulder at 1.15x
        float hypertrophyProgress = 1.0f - MathF.Exp(-0.015f * _meleeSwingCount);
        float armScale = 1.0f + (0.35f * hypertrophyProgress);
        float shoulderScale = 1.0f + (0.15f * hypertrophyProgress);

        // 2. Elemental Ocular & Dermal Resonance
        int totalCasts = _elementalCastCounts.Values.Sum();
        int fireCasts = _elementalCastCounts[MagicElement.Fire];
        int coldCasts = _elementalCastCounts[MagicElement.Cold];
        int shockCasts = _elementalCastCounts[MagicElement.Shock];
        int rotCasts = _elementalCastCounts[MagicElement.RotSap];

        Vector3f ocularColor = new(0.92f, 0.75f, 0.60f); // Default mortal eye color
        float ocularIntensity = Math.Clamp(_recentMagicCharge + (totalCasts * 0.01f), 0f, 1.0f);
        float veinGlow = Math.Clamp(_recentMagicCharge * 0.8f, 0f, 1.0f);

        if (totalCasts > 0)
        {
            float total = (float)totalCasts;
            float r = (fireCasts * 1.0f + shockCasts * 0.75f + rotCasts * 0.2f) / total;
            float g = (fireCasts * 0.35f + coldCasts * 0.85f + rotCasts * 0.95f) / total;
            float b = (coldCasts * 1.0f + shockCasts * 0.95f + rotCasts * 0.1f) / total;

            // Normalize color vector
            float maxC = MathF.Max(r, MathF.Max(g, b));
            if (maxC > 0.001f)
            {
                ocularColor = new Vector3f(r / maxC, g / maxC, b / maxC);
            }
        }

        // 3. Stanza Spell Evolution Tier (Practice polishes somatic casting flow)
        int evolutionTier = totalCasts switch
        {
            >= 100 => 4, // Transcendent Prismatic Spellmaster
            >= 40 => 3,  // Master Thaumaturge
            >= 15 => 2,  // Adept Elementalist
            >= 5 => 1,   // Neophyte Channeler
            _ => 0       // Novice
        };

        float castSpeedBonus = Math.Clamp(evolutionTier * 0.05f, 0f, 0.20f); // Up to 20% cast time reduction

        // Visual summary
        string desc = $"Arm Hypertrophy: +{(hypertrophyProgress * 35):F0}% | Ocular Resonance: {ocularIntensity * 100:F0}% ({_dominantElement}) | Spell Tier: {evolutionTier}";

        return new MorphologicalState(
            DominantArmScale: armScale,
            ShoulderBreadthScale: shoulderScale,
            OcularGlowIntensity: ocularIntensity,
            OcularColor: ocularColor,
            DermalVeinGlow: veinGlow,
            SpellEvolutionTier: evolutionTier,
            CastSpeedBonus: castSpeedBonus,
            VisualDescription: desc
        );
    }
}
