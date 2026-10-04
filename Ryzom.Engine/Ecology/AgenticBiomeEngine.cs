using System.Text.Json;
using Ryzom.Core.Math3D;

namespace Ryzom.Engine.Ecology;

public enum FaunaThreatLevel
{
    DocileHerbivore,
    SkittishGrazer,
    HostilePredator,
    ApexKitinWarden
}

public class DynamicCreaturePack
{
    public string PackId { get; set; } = Guid.NewGuid().ToString("N")[..6];
    public string Species { get; set; } = "Yubo";
    public FaunaThreatLevel ThreatLevel { get; set; } = FaunaThreatLevel.DocileHerbivore;
    public int Count { get; set; } = 6;
    public Vector3f CurrentPosition { get; set; }
    public Vector3f TargetPosition { get; set; }
    public float MigrationSpeed { get; set; } = 4.0f;
    public string CurrentState { get; set; } = "Grazing";
}

public class AgenticBiomeInstance
{
    public string BiomeId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public float CanopyIntegrity { get; set; } = 1.0f;
    public float MaxTimberVolumeM3 { get; set; } = 12000f;
    public float CurrentTimberVolumeM3 { get; set; } = 12000f;
    public float DisturbanceStress { get; set; } = 0.0f;
    public float MycorrhizalDistressSignal { get; set; } = 0.0f;
    public string ActiveAffordance { get; set; } = "SustainPeacefulCanopy";
    public Dictionary<string, float> AffordanceScores { get; } = new();
    public List<DynamicCreaturePack> Packs { get; } = new();
    public List<string> EpisodicMemory { get; } = new();

    // Neural map weights: affordance -> (sensory_input -> weight)
    public Dictionary<string, Dictionary<string, float>> AffordanceWeights { get; } = new();

    public void LoadFromDirectory(string biomeDirectoryPath)
    {
        string neuralPath = Path.Combine(biomeDirectoryPath, "NEURAL_MAP.json");
        if (File.Exists(neuralPath))
        {
            string json = File.ReadAllText(neuralPath);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("biome_id", out var idElem))
            {
                BiomeId = idElem.GetString() ?? "";
            }

            if (root.TryGetProperty("affordance_weights", out var weightsElem))
            {
                foreach (var affProp in weightsElem.EnumerateObject())
                {
                    var map = new Dictionary<string, float>();
                    foreach (var inputProp in affProp.Value.EnumerateObject())
                    {
                        map[inputProp.Name] = (float)inputProp.Value.GetDouble();
                    }
                    AffordanceWeights[affProp.Name] = map;
                }
            }
        }

        string memoryPath = Path.Combine(biomeDirectoryPath, "MEMORY.md");
        if (File.Exists(memoryPath))
        {
            var lines = File.ReadAllLines(memoryPath);
            foreach (var line in lines)
            {
                if (line.StartsWith("- [") && !string.IsNullOrWhiteSpace(line))
                {
                    EpisodicMemory.Add(line.Trim());
                }
            }
        }
    }

    public void EvaluateAffordances(Dictionary<string, float> sensoryInputs)
    {
        AffordanceScores.Clear();
        string bestAffordance = ActiveAffordance;
        float highestScore = float.MinValue;

        foreach (var (affordance, weights) in AffordanceWeights)
        {
            float score = 0f;
            foreach (var (stimulus, weight) in weights)
            {
                if (sensoryInputs.TryGetValue(stimulus, out float val))
                {
                    score += val * weight;
                }
            }
            AffordanceScores[affordance] = score;

            if (score > highestScore)
            {
                highestScore = score;
                bestAffordance = affordance;
            }
        }

        ActiveAffordance = bestAffordance;
    }
}

public class AgenticBiomeEngine
{
    private readonly AgenticBiomeInstance _startingVillage;
    private readonly AgenticBiomeInstance _primeRootsFrontier;

    public AgenticBiomeInstance StartingVillage => _startingVillage;
    public AgenticBiomeInstance PrimeRootsFrontier => _primeRootsFrontier;

    public AgenticBiomeEngine(string baseBiomesDirectory)
    {
        _startingVillage = new AgenticBiomeInstance
        {
            BiomeId = "verdant_verge",
            DisplayName = "Verdant Verge (Starting Outpost)"
        };
        _startingVillage.LoadFromDirectory(Path.Combine(baseBiomesDirectory, "VerdantVerge_StartingVillage"));
        InitializeVillageFauna();

        _primeRootsFrontier = new AgenticBiomeInstance
        {
            BiomeId = "prime_roots_frontier",
            DisplayName = "Prime Roots Frontier"
        };
        _primeRootsFrontier.LoadFromDirectory(Path.Combine(baseBiomesDirectory, "PrimeRoots_Frontier"));
        InitializeFrontierFauna();
    }

    private void InitializeVillageFauna()
    {
        _startingVillage.Packs.Add(new DynamicCreaturePack
        {
            Species = "Yubo",
            ThreatLevel = FaunaThreatLevel.DocileHerbivore,
            Count = 8,
            CurrentPosition = new Vector3f(25f, 0f, 30f),
            TargetPosition = new Vector3f(25f, 0f, 30f),
            CurrentState = "Peaceful Grazing"
        });

        _startingVillage.Packs.Add(new DynamicCreaturePack
        {
            Species = "Mektoub Calf",
            ThreatLevel = FaunaThreatLevel.SkittishGrazer,
            Count = 4,
            CurrentPosition = new Vector3f(-40f, 0f, 20f),
            TargetPosition = new Vector3f(-40f, 0f, 20f),
            CurrentState = "Resting near Village Stream"
        });
    }

    private void InitializeFrontierFauna()
    {
        _primeRootsFrontier.Packs.Add(new DynamicCreaturePack
        {
            Species = "Armored Kitin Harvester (Lvl 45)",
            ThreatLevel = FaunaThreatLevel.ApexKitinWarden,
            Count = 3,
            CurrentPosition = new Vector3f(450f, -20f, 400f),
            TargetPosition = new Vector3f(450f, -20f, 400f),
            CurrentState = "Subterranean Dormancy"
        });

        _primeRootsFrontier.Packs.Add(new DynamicCreaturePack
        {
            Species = "Apex Kincher (Lvl 50)",
            ThreatLevel = FaunaThreatLevel.HostilePredator,
            Count = 2,
            CurrentPosition = new Vector3f(380f, 0f, 320f),
            TargetPosition = new Vector3f(380f, 0f, 320f),
            CurrentState = "Prowling Deep Bark Chasms"
        });
    }

    public void HarvestVillageTimber(float volumeM3, int treesFelled, float worldTimeHours)
    {
        _startingVillage.CurrentTimberVolumeM3 = MathF.Max(0f, _startingVillage.CurrentTimberVolumeM3 - volumeM3);
        _startingVillage.CanopyIntegrity = _startingVillage.CurrentTimberVolumeM3 / _startingVillage.MaxTimberVolumeM3;
        _startingVillage.DisturbanceStress = Math.Clamp(1.0f - _startingVillage.CanopyIntegrity, 0f, 1f);

        var stimuli = new Dictionary<string, float>
        {
            ["timber_harvest_volume"] = Math.Clamp(volumeM3 / 2000f, 0f, 1f),
            ["felling_vibrations"] = Math.Clamp(treesFelled / 5f, 0f, 1f),
            ["canopy_loss_ratio"] = _startingVillage.DisturbanceStress,
            ["repose_duration"] = 0f
        };

        _startingVillage.EvaluateAffordances(stimuli);

        // Record episodic memory
        string eventMsg = $"[Hour {worldTimeHours:F1}] Harvested {volumeM3:F0}m³ ({treesFelled} trees). Canopy down to {_startingVillage.CanopyIntegrity * 100:F1}%. Affordance: {_startingVillage.ActiveAffordance}";
        _startingVillage.EpisodicMemory.Add(eventMsg);

        // Execute affordance effects
        if (_startingVillage.ActiveAffordance == "DisplaceFaunaToPeriphery" || _startingVillage.DisturbanceStress > 0.25f)
        {
            // Displace gentle herbivores toward the frontier buffer (X: 180 to 220)
            foreach (var pack in _startingVillage.Packs)
            {
                pack.TargetPosition = new Vector3f(180f + (pack.Count * 5f), 0f, 190f);
                pack.CurrentState = "Fleeing Deforestation into Wild Frontier";
                pack.CurrentPosition = Vector3f.Lerp(pack.CurrentPosition, pack.TargetPosition, 0.45f);
            }
        }

        if (_startingVillage.DisturbanceStress > 0.35f)
        {
            // Propagate mycorrhizal distress to Prime Roots Frontier
            _startingVillage.MycorrhizalDistressSignal = _startingVillage.DisturbanceStress;
            PropagateDistressToFrontier(worldTimeHours);
        }
    }

    private void PropagateDistressToFrontier(float worldTimeHours)
    {
        float distress = _startingVillage.MycorrhizalDistressSignal;
        _primeRootsFrontier.MycorrhizalDistressSignal = distress;

        var stimuli = new Dictionary<string, float>
        {
            ["incoming_root_distress"] = distress,
            ["displaced_prey_scent"] = _startingVillage.DisturbanceStress > 0.3f ? 0.95f : 0.2f,
            ["sap_hemorrhage_volume"] = distress * 0.85f,
            ["surface_repose"] = 0f
        };

        _primeRootsFrontier.EvaluateAffordances(stimuli);

        string frontierMsg = $"[Hour {worldTimeHours:F1}] Subterranean distress pulse received (amplitude: {distress:F2}). Affordance activated: {_primeRootsFrontier.ActiveAffordance}";
        _primeRootsFrontier.EpisodicMemory.Add(frontierMsg);

        // Execute frontier affordances: Migrate predators towards the village perimeter
        if (_primeRootsFrontier.ActiveAffordance == "MobilizeKitinPatrol" || _primeRootsFrontier.ActiveAffordance == "ApexCarnivoreMigration")
        {
            foreach (var pack in _primeRootsFrontier.Packs)
            {
                if (pack.ThreatLevel == FaunaThreatLevel.ApexKitinWarden)
                {
                    // Encroaches to the deforested border at (160, 0, 150)
                    pack.TargetPosition = new Vector3f(160f, 0f, 150f);
                    pack.CurrentState = "Surfacing from Prime Roots to Investigate Deforestation";
                    pack.CurrentPosition = Vector3f.Lerp(pack.CurrentPosition, pack.TargetPosition, 0.40f);
                }
                else if (pack.ThreatLevel == FaunaThreatLevel.HostilePredator)
                {
                    // Pursues fleeing herbivores into the buffer at (190, 0, 180)
                    pack.TargetPosition = new Vector3f(190f, 0f, 180f);
                    pack.CurrentState = "Hunting Displaced Yubo Packs near Village Perimeter";
                    pack.CurrentPosition = Vector3f.Lerp(pack.CurrentPosition, pack.TargetPosition, 0.35f);
                }
            }
        }
    }

    public void EcologicalTick(float deltaHours, float worldTimeHours)
    {
        // Gradual Swanson allometric regrowth if harvesting has ceased
        if (_startingVillage.CanopyIntegrity < 1.0f)
        {
            float recoveryRate = 0.035f * deltaHours;
            _startingVillage.CanopyIntegrity = Math.Clamp(_startingVillage.CanopyIntegrity + recoveryRate, 0f, 1.0f);
            _startingVillage.CurrentTimberVolumeM3 = _startingVillage.CanopyIntegrity * _startingVillage.MaxTimberVolumeM3;
            _startingVillage.DisturbanceStress = Math.Clamp(_startingVillage.DisturbanceStress - (recoveryRate * 1.5f), 0f, 1f);

            var stimuli = new Dictionary<string, float>
            {
                ["timber_harvest_volume"] = 0f,
                ["felling_vibrations"] = 0f,
                ["canopy_loss_ratio"] = _startingVillage.DisturbanceStress,
                ["repose_duration"] = Math.Clamp(deltaHours / 12f, 0f, 1f)
            };

            _startingVillage.EvaluateAffordances(stimuli);

            if (_startingVillage.DisturbanceStress < 0.20f)
            {
                // Calm herbivores return to central village stream
                foreach (var pack in _startingVillage.Packs)
                {
                    if (pack.Species == "Yubo") pack.TargetPosition = new Vector3f(25f, 0f, 30f);
                    if (pack.Species.StartsWith("Mektoub")) pack.TargetPosition = new Vector3f(-40f, 0f, 20f);
                    pack.CurrentState = "Returning to Restored Canopy Nursery";
                    pack.CurrentPosition = Vector3f.Lerp(pack.CurrentPosition, pack.TargetPosition, 0.30f);
                }

                // Prime Roots predators retreat
                _primeRootsFrontier.MycorrhizalDistressSignal = 0f;
                var frontierStimuli = new Dictionary<string, float>
                {
                    ["incoming_root_distress"] = 0f,
                    ["displaced_prey_scent"] = 0.1f,
                    ["sap_hemorrhage_volume"] = 0f,
                    ["surface_repose"] = 1.0f
                };
                _primeRootsFrontier.EvaluateAffordances(frontierStimuli);

                foreach (var pack in _primeRootsFrontier.Packs)
                {
                    if (pack.ThreatLevel == FaunaThreatLevel.ApexKitinWarden)
                    {
                        pack.TargetPosition = new Vector3f(450f, -20f, 400f);
                        pack.CurrentState = "Retreating to Subterranean Tunnels";
                    }
                    else
                    {
                        pack.TargetPosition = new Vector3f(380f, 0f, 320f);
                        pack.CurrentState = "Retreating to Deep Prime Roots Chasms";
                    }
                    pack.CurrentPosition = Vector3f.Lerp(pack.CurrentPosition, pack.TargetPosition, 0.30f);
                }
            }
        }
    }
}
