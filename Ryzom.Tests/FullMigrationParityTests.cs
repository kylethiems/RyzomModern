using System.Collections.Generic;
using System.Numerics;
using Ryzom.Engine.Animation;
using Ryzom.Engine.Entities;
using Ryzom.Engine.Landscape;
using Ryzom.Engine.Stanza;
using Xunit;

namespace Ryzom.Tests;

public class FullMigrationParityTests
{
    [Fact]
    public void SkeletalAnimation_EvaluatesKeyframeInterpolation_AndSkinning()
    {
        var engine = new NeLSkeletonAnimationEngine();

        // Build 2-bone arm: Bone 0 (Root/Shoulder), Bone 1 (Forearm)
        engine.AddBone(new SkeletalBone(0, "Shoulder", -1, Vector3.Zero, Quaternion.Identity, Matrix4x4.Identity));
        engine.AddBone(new SkeletalBone(1, "Forearm", 0, new Vector3(0, 1, 0), Quaternion.Identity, Matrix4x4.Identity));

        // Create animation clip
        var track1 = new List<BoneKeyframe>
        {
            new(0.0f, new Vector3(0, 1, 0), Quaternion.Identity, Vector3.One),
            new(1.0f, new Vector3(0, 1.5f, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 4f), Vector3.One)
        };

        var clip = new AnimationClip("SwingAxe", 1.0f, true, new Dictionary<int, List<BoneKeyframe>> { [1] = track1 });

        // Evaluate at t = 0.5s
        var pose = engine.EvaluateSkeletonPose(clip, 0.5f);
        Assert.Equal(2, pose.Length);

        // Test vertex skinning
        var weight = new VertexSkinningWeight((0, 1, 0, 0), (0.2f, 0.8f, 0f, 0f));
        var skinned = engine.SkinVertex(new Vector3(0, 1, 0), pose, weight);
        Assert.NotEqual(Vector3.Zero, skinned);
    }

    [Fact]
    public void LandscapeQuadtree_SamplesElevation_AndSplatWeights()
    {
        var desertEngine = new NeLLandscapeQuadtreeEngine(AtysContinent.BurningDesert);
        var forestEngine = new NeLLandscapeQuadtreeEngine(AtysContinent.VerdantHeights);

        float desertH = desertEngine.GetElevation(50f, 50f);
        float forestH = forestEngine.GetElevation(50f, 50f);

        Assert.True(desertH > 0f);
        Assert.True(forestH > 0f);

        // Splat weights
        var desertSplat = desertEngine.CalculateSplatWeights(50f, 50f);
        Assert.True(desertSplat.SandDune > 0.4f, "Desert must have high sand dune weight");

        var forestSplat = forestEngine.CalculateSplatWeights(50f, 50f);
        Assert.True(forestSplat.MossyBark > 0.2f || forestSplat.FertileLoam > 0.2f);

        // Terrain clamping
        var clamped = forestEngine.ClampToTerrain(new Vector3(10, 0, 10), verticalOffset: 1.0f);
        Assert.Equal(forestEngine.GetElevation(10, 10) + 1.0f, clamped.Y);
    }

    [Fact]
    public void StanzaCompiler_EnforcesCreditBalanceRules_AndCompilesValidAction()
    {
        var compiler = new StanzaActionGrammarCompiler();

        // 1. Valid fireball spell: Target + Fire Damage I (10 pts) + Sap Credit 20 (10 pts)
        var validSpell = compiler.Compile("Fireball I", new[] { "target_single", "effect_fire_direct", "cost_sap_20" });
        Assert.True(validSpell.IsValid);
        Assert.Null(validSpell.ValidationError);
        Assert.Equal(20, validSpell.TotalSapCost);
        Assert.Equal(20f, validSpell.FinalRangeMeters);

        // 2. Invalid fireball spell: Target + Shock Damage (12 pts) + Sap Credit 20 (only 10 pts) -> under-credited
        var invalidSpell = compiler.Compile("Overpowered Shock", new[] { "target_single", "effect_shock_direct", "cost_sap_20" });
        Assert.False(invalidSpell.IsValid);
        Assert.Contains("Insufficient Cost Credit", invalidSpell.ValidationError);
    }

    [Fact]
    public void Paperdoll_EquipsSlots_AndComputesArmorAndWeight()
    {
        var paperdoll = new PaperdollEquipmentEngine();

        var cuirass = new GearItem("cuirass_matis_q100", "Seasoned Bark Cuirass", EquipmentSlot.Chest,
            MaterialQualityTier.SeasonedHeartwoodQ100, 85, 30, 4.5f, "mesh_matis_heavy_chest");

        var greatsword = new GearItem("blade_fyros_q200", "Prime Roots Greatsword", EquipmentSlot.TwoHanded,
            MaterialQualityTier.PrimeRootsAmberQ200, 20, 10, 8.0f, "mesh_fyros_2h_blade");

        bool eq1 = paperdoll.Equip(cuirass, out _);
        bool eq2 = paperdoll.Equip(greatsword, out _);

        Assert.True(eq1 && eq2);
        Assert.Equal(2, paperdoll.EquippedCount);

        var stats = paperdoll.CalculateTotalStats();
        Assert.Equal(105, stats.TotalArmorRating);
        Assert.Equal(40, stats.TotalElementalResist);
        Assert.Equal(12.5f, stats.TotalWeightKg);
        Assert.Equal(0f, stats.EncumbrancePenaltyPercent); // under 15kg threshold
    }
}
