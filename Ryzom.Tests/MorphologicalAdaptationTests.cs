using Xunit;
using Ryzom.Engine.Mechanics;

namespace Ryzom.Tests;

public class MorphologicalAdaptationTests
{
    [Fact]
    public void Test_MeleeSwings_InduceDominantArmHypertrophy()
    {
        var engine = new MorphologicalAdaptationEngine();
        var initial = engine.EvaluateMorphology();

        Assert.Equal(1.0f, initial.DominantArmScale);
        Assert.Equal(1.0f, initial.ShoulderBreadthScale);

        // Perform 50 heavy melee blade swings
        engine.RecordMeleeSwing(50);
        var postTraining = engine.EvaluateMorphology();

        Assert.True(postTraining.DominantArmScale > 1.15f, "Arm scale should enlarge with repeated swings");
        Assert.True(postTraining.ShoulderBreadthScale > 1.05f, "Shoulder breadth should expand");
        Assert.True(postTraining.DominantArmScale <= 1.35f, "Arm scale should not exceed allometric cap of 1.35x");
    }

    [Fact]
    public void Test_ElementalSpellCasting_ShiftsOcularHueAndResonance()
    {
        var engine = new MorphologicalAdaptationEngine();

        // Cast 20 Fireballs
        engine.RecordSpellCast(MagicElement.Fire, 20);
        var fireState = engine.EvaluateMorphology();

        Assert.True(fireState.OcularGlowIntensity > 0.5f, "Intense spellcasting should ignite ocular glow");
        Assert.True(fireState.OcularColor.X > fireState.OcularColor.Z, "Fire ocular color should have high red/amber over blue");

        // Now cast 40 Ice / Cold shards
        engine.RecordSpellCast(MagicElement.Cold, 40);
        var coldState = engine.EvaluateMorphology();

        Assert.True(coldState.OcularColor.Z > 0.7f, "Cold ocular color should have strong cyan/blue resonance");
    }

    [Fact]
    public void Test_SpellPractice_UnlocksEvolutionTiersAndSpeedBonus()
    {
        var engine = new MorphologicalAdaptationEngine();

        Assert.Equal(0, engine.EvaluateMorphology().SpellEvolutionTier);
        Assert.Equal(0f, engine.EvaluateMorphology().CastSpeedBonus);

        // Practice 15 spells -> Adept tier
        engine.RecordSpellCast(MagicElement.Shock, 15);
        var adeptState = engine.EvaluateMorphology();
        Assert.Equal(2, adeptState.SpellEvolutionTier);
        Assert.True(adeptState.CastSpeedBonus >= 0.10f);

        // Practice 100 spells -> Transcendent Master tier
        engine.RecordSpellCast(MagicElement.Fire, 85);
        var masterState = engine.EvaluateMorphology();
        Assert.Equal(4, masterState.SpellEvolutionTier);
        Assert.Equal(0.20f, masterState.CastSpeedBonus);
    }

    [Fact]
    public void Test_MetabolicDecay_CoolsMagicCharge()
    {
        var engine = new MorphologicalAdaptationEngine();
        engine.RecordSpellCast(MagicElement.Fire, 5);

        float initialGlow = engine.EvaluateMorphology().OcularGlowIntensity;

        // 4 hours of inactivity
        engine.TickMetabolicDecay(4.0f);
        float cooledGlow = engine.EvaluateMorphology().OcularGlowIntensity;

        Assert.True(cooledGlow < initialGlow, "Elemental ocular glow should cool over resting hours");
    }
}
