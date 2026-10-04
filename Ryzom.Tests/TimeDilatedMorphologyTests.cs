using Xunit;
using Ryzom.Engine.Mechanics;

namespace Ryzom.Tests;

public class TimeDilatedMorphologyTests
{
    [Fact]
    public void Test_YouthfulStartingState_HasBaselinePristineMorphology()
    {
        var engine = new TimeDilatedMorphologyEngine(20.0f);
        var state = engine.EvaluateMorphology();

        Assert.Equal(20.0f, state.ChronologicalAgeYears);
        Assert.Equal(1.0f, state.DominantArmScale);
        Assert.Equal(1.0f, state.ShoulderBreadthScale);
        Assert.Equal(0.0f, state.SkinWeatheringPatina);
        Assert.Equal(0.0f, state.HairMelaninGreying);
        Assert.Equal(0, state.AccumulatedMicroScars);
        Assert.Contains("Youthful", state.VisualDescription);
    }

    [Fact]
    public void Test_TimeDilationCareer_SmoothlyMorphsNpcOver15Years()
    {
        var engine = new TimeDilatedMorphologyEngine(20.0f);
        var daxCareer = new LifeCycleCareerProfile(
            RoleName: "ForemanDax_TimberOverseer",
            DailySwingsAverage: 120f,
            DailySpellCastsAverage: 5f,
            FavoredElement: MagicElement.Fire,
            DailySunExposureHours: 8.5f,
            AnnualCombatSkirmishes: 8
        );

        // Simulate 15 years of career life-cycle
        engine.SimulateTimeDilation(15.0f, daxCareer);
        var state = engine.EvaluateMorphology();

        Assert.Equal(35.0f, state.ChronologicalAgeYears);
        // Arm hypertrophy should be noticeable but realistic (+15% to +25%)
        Assert.True(state.DominantArmScale > 1.15f && state.DominantArmScale < 1.28f,
            $"Arm scale should be seasoned (+15-28%), actual: {state.DominantArmScale}");
        // Desert sun exposure creates realistic patina
        Assert.True(state.SkinWeatheringPatina > 0.40f, "Sun weathering should accumulate over 15 years");
        // Micro-scars from annual caravan guard skirmishes
        Assert.True(state.AccumulatedMicroScars >= 20, "Should have accumulated combat micro-scars over 15 years");
        Assert.Contains("Seasoned", state.VisualDescription);
    }

    [Fact]
    public void Test_AdvancedAging_InducesHairGreyingAndPosturalCompression()
    {
        var engine = new TimeDilatedMorphologyEngine(20.0f);
        var veteranCareer = new LifeCycleCareerProfile(
            RoleName: "VeteranSentinel",
            DailySwingsAverage: 80f,
            DailySpellCastsAverage: 10f,
            FavoredElement: MagicElement.None,
            DailySunExposureHours: 7.0f,
            AnnualCombatSkirmishes: 4
        );

        // Simulate 35 years (reaching age 55)
        engine.SimulateTimeDilation(35.0f, veteranCareer);
        var state = engine.EvaluateMorphology();

        Assert.Equal(55.0f, state.ChronologicalAgeYears);
        Assert.True(state.HairMelaninGreying > 0.60f, "Hair greying should be pronounced at age 55");
        Assert.True(state.PosturalCompression > 0.03f, "Subtle postural compression should develop under veteran armor");
        Assert.Contains("Grizzled", state.VisualDescription);
    }

    [Fact]
    public void Test_LivePlayerActions_ProduceSubtleMicroSteps()
    {
        var engine = new TimeDilatedMorphologyEngine(22.0f);
        var initial = engine.EvaluateMorphology();

        // 1 single swing during gameplay
        engine.FineTunePlayerAction("MeleeSwing", 1.0f);
        var afterOneSwing = engine.EvaluateMorphology();

        float delta = afterOneSwing.DominantArmScale - initial.DominantArmScale;
        Assert.True(delta < 0.0001f, "Single player actions must be subtle and non-dramatic");
        Assert.True(afterOneSwing.DominantArmScale >= initial.DominantArmScale);
    }
}
