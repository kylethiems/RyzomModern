using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ryzom.Engine.AI
{
    public record NpcSensoryInput(
        double TimberHarvested,
        double HighStumpageValue,
        double DeforestationSpike,
        double KitinTremor,
        double PlayerProximity
    );

    public record EvaluatedAffordance(
        string Name,
        double UtilityScore,
        string ActionSummary,
        string FormattedDialogue
    );

    public record NpcNeuralMapConfig(
        string NpcId,
        Dictionary<string, double> BaseMood,
        List<string> SensoryInputs,
        Dictionary<string, Dictionary<string, double>> AffordanceWeights
    );

    public class AgenticNpcInstance
    {
        public string NpcId { get; }
        public string DirectoryPath { get; }
        public string PersonaMarkdown { get; private set; } = string.Empty;
        public string AffordancesMarkdown { get; private set; } = string.Empty;
        public NpcNeuralMapConfig NeuralMap { get; private set; } = new(string.Empty, new(), new(), new());
        public List<string> EpisodicMemoryLog { get; } = new();

        public NpcSensoryInput CurrentSensoryState { get; private set; } = new(0, 0, 0, 0, 1.0);
        public EvaluatedAffordance ActiveAffordance { get; private set; } = new(string.Empty, 0, string.Empty, string.Empty);
        public SomaticProfile Somatic { get; } = new();

        public AgenticNpcInstance(string directoryPath)
        {
            DirectoryPath = directoryPath;
            NpcId = Path.GetFileName(directoryPath);
            ReloadFromDisk();
        }

        public void ReloadFromDisk()
        {
            string personaPath = Path.Combine(DirectoryPath, "PERSONA.md");
            if (File.Exists(personaPath)) PersonaMarkdown = File.ReadAllText(personaPath);

            string affordancePath = Path.Combine(DirectoryPath, "AFFORDANCES.md");
            if (File.Exists(affordancePath)) AffordancesMarkdown = File.ReadAllText(affordancePath);

            string neuralPath = Path.Combine(DirectoryPath, "NEURAL_MAP.json");
            if (File.Exists(neuralPath))
            {
                string json = File.ReadAllText(neuralPath);
                NeuralMap = JsonSerializer.Deserialize<NpcNeuralMapConfig>(json, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })
                    ?? throw new InvalidOperationException("Failed to parse NEURAL_MAP.json");
            }
            else
            {
                NeuralMap = new NpcNeuralMapConfig(NpcId, new(), new(), new());
            }

            string memoryPath = Path.Combine(DirectoryPath, "MEMORY.md");
            if (File.Exists(memoryPath))
            {
                EpisodicMemoryLog.Clear();
                EpisodicMemoryLog.AddRange(File.ReadAllLines(memoryPath));
            }

            EvaluateNeuralMapping(new Dictionary<string, string>());
        }

        public List<EvaluatedAffordance> IngestWorldEvent(
            string eventName,
            Dictionary<string, double> stimulusDeltas,
            Dictionary<string, string> contextTokens)
        {
            // Update sensory state
            double timber = Math.Clamp(CurrentSensoryState.TimberHarvested + stimulusDeltas.GetValueOrDefault("timber_harvested", 0.0), 0.0, 1.0);
            double stumpage = Math.Clamp(CurrentSensoryState.HighStumpageValue + stimulusDeltas.GetValueOrDefault("high_stumpage_value", 0.0), 0.0, 1.0);
            double deforest = Math.Clamp(CurrentSensoryState.DeforestationSpike + stimulusDeltas.GetValueOrDefault("deforestation_spike", 0.0), 0.0, 1.0);
            double tremor = Math.Clamp(CurrentSensoryState.KitinTremor + stimulusDeltas.GetValueOrDefault("kitin_tremor", 0.0), 0.0, 1.0);
            double proximity = Math.Clamp(CurrentSensoryState.PlayerProximity + stimulusDeltas.GetValueOrDefault("player_proximity", 0.0), 0.0, 1.0);

            CurrentSensoryState = new NpcSensoryInput(timber, stumpage, deforest, tremor, proximity);

            // Append to episodic memory
            string timestamp = DateTime.UtcNow.ToString("HH:mm:ss UTC");
            string memoryEntry = $"- `[{timestamp}]` Event '{eventName}' received. Sensory state: Timber={timber:F2}, Tremor={tremor:F2}, Deforest={deforest:F2}.";
            EpisodicMemoryLog.Add(memoryEntry);

            return EvaluateNeuralMapping(contextTokens);
        }

        public List<EvaluatedAffordance> EvaluateNeuralMapping(Dictionary<string, string> tokens)
        {
            var ranked = new List<EvaluatedAffordance>();
            if (NeuralMap?.AffordanceWeights == null) return ranked;

            foreach (var (affordanceName, weights) in NeuralMap.AffordanceWeights)
            {
                // Topological vector dot product
                double score = 0.0;
                score += weights.GetValueOrDefault("timber_harvested", 0.0) * CurrentSensoryState.TimberHarvested;
                score += weights.GetValueOrDefault("high_stumpage_value", 0.0) * CurrentSensoryState.HighStumpageValue;
                score += weights.GetValueOrDefault("deforestation_spike", 0.0) * CurrentSensoryState.DeforestationSpike;
                score += weights.GetValueOrDefault("kitin_tremor", 0.0) * CurrentSensoryState.KitinTremor;
                score += weights.GetValueOrDefault("player_proximity", 0.0) * CurrentSensoryState.PlayerProximity;

                string dialogue = GenerateDialogue(affordanceName, tokens);
                ranked.Add(new EvaluatedAffordance(affordanceName, score, $"Executing {affordanceName} with utility {score:F2}", dialogue));
            }

            ranked = ranked.OrderByDescending(a => a.UtilityScore).ToList();
            if (ranked.Count > 0)
            {
                ActiveAffordance = ranked[0];
            }

            return ranked;
        }

        public List<EvaluatedAffordance> RecordCombatInjury(
            InjuryType type,
            InjuryLocation location,
            double severity,
            DateTime timestampUtc)
        {
            Somatic.InflictInjury(type, location, severity, timestampUtc);

            string timestamp = timestampUtc.ToString("HH:mm:ss UTC");
            string entry = $"- `[{timestamp}]` COMBAT WOUND: Sustained {type} on {location} (Severity {severity:F1}). Armor scarred.";
            EpisodicMemoryLog.Add(entry);

            // Somatic feedback loop into sensory state (increases vigilance & paranoia)
            var deltas = new Dictionary<string, double>
            {
                ["kitin_tremor"] = 0.6,
                ["deforestation_spike"] = 0.4
            };
            return IngestWorldEvent("CombatAmbush", deltas, new());
        }

        private string GenerateDialogue(string affordanceName, Dictionary<string, string> tokens)
        {
            var somaticState = Somatic.GetAggregateVisualState(DateTime.UtcNow);

            string volume = tokens.GetValueOrDefault("volume", "245");
            string grade = tokens.GetValueOrDefault("grade", "Seasoned Heartwood");
            string profit = tokens.GetValueOrDefault("profit", "80,830");
            string haul = tokens.GetValueOrDefault("haul", "84.83");
            string km = tokens.GetValueOrDefault("km", "4.5");

            // Somatic overlay for fresh wounds
            if (somaticState.BleedSeverity > 0.4)
            {
                return $"*Clutches {somaticState.VisualDescription}* By the Kami, that mandible tore straight through my harness! Still... {volume} MBF of {grade} was worth the blood.";
            }

            // Somatic overlay for healed veteran scars
            if (somaticState.PermanentScarFactor > 0.25 && affordanceName == "IdlePatrol")
            {
                return "*Traces silvery battle scar across armor plate* The Kitin raiders left their mark at the pass, but the caravan held the line.";
            }

            return affordanceName switch
            {
                "AppraiseFelledTimber" => $"By the Great Root! That's {volume} MBF of {grade}. I'll buy it for ${profit} delivered to the Pyr outpost!",
                "DispatchMektoubCaravan" => $"Rigging harness on two heavy Mektoubs. Haul cost is ${haul}/MBF for the {km}km transit.",
                "WarnKitinPheromones" => "Halt the blades! The soil is trembling. You've stripped too much root bark—the Kitin swarm is stirring!",
                "SoundOutpostAlarm" => "ALARM! Mandibles breaching the surface! Outpost militia to arms!",
                "IdlePatrol" => "Keep your blades sharp and your eyes on the sand dunes.",
                _ => "Atys provides for those who observe the bark."
            };
        }
    }
}
