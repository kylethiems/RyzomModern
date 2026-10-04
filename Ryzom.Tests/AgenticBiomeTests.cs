using Xunit;
using Ryzom.Engine.Ecology;

namespace Ryzom.Tests;

public class AgenticBiomeTests
{
    private readonly string _biomesDir;

    public AgenticBiomeTests()
    {
        // Path to Biomes directory relative to test output
        string current = AppContext.BaseDirectory;
        string? projectRoot = null;
        var dir = new DirectoryInfo(current);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "Biomes")))
            {
                projectRoot = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        _biomesDir = projectRoot != null ? Path.Combine(projectRoot, "Biomes") : "";
    }

    [Fact]
    public void Test_BiomeNeuralMapAndMemory_LoadSuccessfully()
    {
        if (string.IsNullOrEmpty(_biomesDir) || !Directory.Exists(_biomesDir))
            return;

        var engine = new AgenticBiomeEngine(_biomesDir);

        Assert.Equal("verdant_verge", engine.StartingVillage.BiomeId);
        Assert.True(engine.StartingVillage.AffordanceWeights.ContainsKey("DisplaceFaunaToPeriphery"));
        Assert.True(engine.StartingVillage.AffordanceWeights.ContainsKey("SignalEcosystemDistress"));
        Assert.NotEmpty(engine.StartingVillage.EpisodicMemory);

        Assert.Equal("prime_roots_frontier", engine.PrimeRootsFrontier.BiomeId);
        Assert.True(engine.PrimeRootsFrontier.AffordanceWeights.ContainsKey("MobilizeKitinPatrol"));
        Assert.True(engine.PrimeRootsFrontier.AffordanceWeights.ContainsKey("ApexCarnivoreMigration"));
    }

    [Fact]
    public void Test_TimberHarvesting_DisplacesHerbivoresToPeriphery()
    {
        if (string.IsNullOrEmpty(_biomesDir) || !Directory.Exists(_biomesDir))
            return;

        var engine = new AgenticBiomeEngine(_biomesDir);
        var initialYuboPos = engine.StartingVillage.Packs.First(p => p.Species == "Yubo").CurrentPosition;

        // Harvest 3,000 m3 of timber (approx 25% of village canopy)
        engine.HarvestVillageTimber(3000f, 6, 2.0f);

        Assert.True(engine.StartingVillage.CanopyIntegrity < 0.80f);
        Assert.Equal("DisplaceFaunaToPeriphery", engine.StartingVillage.ActiveAffordance);

        var yuboPack = engine.StartingVillage.Packs.First(p => p.Species == "Yubo");
        Assert.Contains("Fleeing", yuboPack.CurrentState);
        Assert.True(yuboPack.CurrentPosition.X > initialYuboPos.X, "Herbivores should be displaced outward toward frontier");
    }

    [Fact]
    public void Test_HeavyDeforestation_TriggersPrimeRootsKitinEncroachment()
    {
        if (string.IsNullOrEmpty(_biomesDir) || !Directory.Exists(_biomesDir))
            return;

        var engine = new AgenticBiomeEngine(_biomesDir);
        var initialKitinPos = engine.PrimeRootsFrontier.Packs.First(p => p.ThreatLevel == FaunaThreatLevel.ApexKitinWarden).CurrentPosition;

        // Heavy clear-cut: 6,500 m3 (over 50% canopy loss)
        engine.HarvestVillageTimber(6500f, 15, 4.5f);

        Assert.True(engine.StartingVillage.DisturbanceStress > 0.50f);
        Assert.True(engine.StartingVillage.MycorrhizalDistressSignal > 0.50f);

        // Frontier should receive distress and mobilize
        Assert.True(engine.PrimeRootsFrontier.ActiveAffordance == "MobilizeKitinPatrol" ||
                    engine.PrimeRootsFrontier.ActiveAffordance == "ApexCarnivoreMigration");

        var kitinPack = engine.PrimeRootsFrontier.Packs.First(p => p.ThreatLevel == FaunaThreatLevel.ApexKitinWarden);
        Assert.Contains("Surfacing", kitinPack.CurrentState);
        // Kitin should have migrated closer to village perimeter (X: 160 vs initial 450)
        Assert.True(kitinPack.CurrentPosition.X < initialKitinPos.X, "Kitin should migrate towards the deforested border");
    }

    [Fact]
    public void Test_EcologicalRepose_AllowsRegrowthAndPredatorRetreat()
    {
        if (string.IsNullOrEmpty(_biomesDir) || !Directory.Exists(_biomesDir))
            return;

        var engine = new AgenticBiomeEngine(_biomesDir);

        // Clear-cut then let time pass
        engine.HarvestVillageTimber(5000f, 10, 1.0f);
        float damagedIntegrity = engine.StartingVillage.CanopyIntegrity;

        // Advance 30 hours of quiet repose
        engine.EcologicalTick(30.0f, 31.0f);

        Assert.True(engine.StartingVillage.CanopyIntegrity > damagedIntegrity, "Canopy should regrow over time");
        Assert.True(engine.StartingVillage.DisturbanceStress < 0.20f, "Disturbance stress should drop");

        var kitinPack = engine.PrimeRootsFrontier.Packs.First(p => p.ThreatLevel == FaunaThreatLevel.ApexKitinWarden);
        Assert.Contains("Retreating", kitinPack.CurrentState);
    }
}
