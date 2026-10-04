using System;
using System.Collections.Generic;
using System.IO;
using Ryzom.Engine.AI;
using Xunit;

namespace Ryzom.Tests
{
    public class AgenticNpcTests
    {
        private string GetForemanDaxPath()
        {
            // Path relative to repository root
            string baseDir = AppContext.BaseDirectory;
            string repoRoot = Path.GetFullPath(Path.Combine(baseDir, "../../../.."));
            string npcPath = Path.Combine(repoRoot, "NPCs", "Foreman_Dax");
            return npcPath;
        }

        [Fact]
        public void Test_LoadNpcFromMarkdownAndNeuralConfig()
        {
            string npcDir = GetForemanDaxPath();
            Assert.True(Directory.Exists(npcDir), $"Directory {npcDir} should exist");

            var npc = new AgenticNpcInstance(npcDir);
            Assert.False(string.IsNullOrEmpty(npc.PersonaMarkdown));
            Assert.Contains("Foreman Dax", npc.PersonaMarkdown);
            Assert.NotNull(npc.NeuralMap);
            Assert.Equal("foreman_dax", npc.NeuralMap.NpcId);
            Assert.True(npc.NeuralMap.AffordanceWeights.ContainsKey("AppraiseFelledTimber"));
        }

        [Fact]
        public void Test_ReactToTimberHarvest_SelectsAppraisalAffordance()
        {
            string npcDir = GetForemanDaxPath();
            var npc = new AgenticNpcInstance(npcDir);

            // Initially, idle patrol or low stimulus
            Assert.NotNull(npc.ActiveAffordance);

            // Harvest event occurs
            var stimulus = new Dictionary<string, double>
            {
                ["timber_harvested"] = 1.0,
                ["high_stumpage_value"] = 0.9,
                ["player_proximity"] = 0.8
            };
            var context = new Dictionary<string, string>
            {
                ["volume"] = "245",
                ["grade"] = "Seasoned Heartwood",
                ["profit"] = "80,830"
            };

            var ranked = npc.IngestWorldEvent("TimberFelled", stimulus, context);

            Assert.NotEmpty(ranked);
            Assert.Equal("AppraiseFelledTimber", npc.ActiveAffordance.Name);
            Assert.Contains("Seasoned Heartwood", npc.ActiveAffordance.FormattedDialogue);
            Assert.Contains("$80,830", npc.ActiveAffordance.FormattedDialogue);
        }

        [Fact]
        public void Test_ReactToKitinTremor_OverridesToAlarmAffordance()
        {
            string npcDir = GetForemanDaxPath();
            var npc = new AgenticNpcInstance(npcDir);

            // High tremor & deforestation event
            var stimulus = new Dictionary<string, double>
            {
                ["kitin_tremor"] = 1.0,
                ["deforestation_spike"] = 0.9,
                ["player_proximity"] = 0.8
            };

            var ranked = npc.IngestWorldEvent("KitinTremorDetected", stimulus, new());

            Assert.NotEmpty(ranked);
            // SoundOutpostAlarm has kitin_tremor weight of 0.98 vs Appraise -0.80
            Assert.Equal("SoundOutpostAlarm", npc.ActiveAffordance.Name);
            Assert.Contains("ALARM! Mandibles breaching the surface!", npc.ActiveAffordance.FormattedDialogue);
        }

        [Fact]
        public void Test_RealTimeEpisodicMemoryLogging()
        {
            string npcDir = GetForemanDaxPath();
            var npc = new AgenticNpcInstance(npcDir);
            int initialCount = npc.EpisodicMemoryLog.Count;

            npc.IngestWorldEvent("MektoubArrival", new() { ["high_stumpage_value"] = 0.5 }, new());
            Assert.True(npc.EpisodicMemoryLog.Count > initialCount);
            Assert.Contains("MektoubArrival", npc.EpisodicMemoryLog[^1]);
        }

        [Fact]
        public void Test_SomaticInjury_InflictAndAllometricRecovery()
        {
            var profile = new SomaticProfile();
            var t0 = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);

            profile.InflictInjury(InjuryType.KitinMandibleSlash, InjuryLocation.ChestPlate, 3.0, t0);

            // T0: Fresh open wound
            var state0 = profile.GetAggregateVisualState(t0);
            Assert.True(state0.BleedSeverity > 0.8);
            Assert.True(state0.NeedsBandage);
            Assert.Contains("Fresh open", state0.VisualDescription);

            // T0 + 24h: Scab and granulating tissue
            var state24 = profile.GetAggregateVisualState(t0.AddHours(24));
            Assert.True(state24.BleedSeverity < state0.BleedSeverity);
            Assert.True(state24.PermanentScarFactor > 0.1);

            // T0 + 120h: Healed permanent silvery battle scar
            var state120 = profile.GetAggregateVisualState(t0.AddHours(120));
            Assert.Equal(0.0, state120.BleedSeverity);
            Assert.False(state120.NeedsBandage);
            Assert.True(state120.PermanentScarFactor >= 0.4);
            Assert.Contains("Healed silvery battle scar", state120.VisualDescription);
        }

        [Fact]
        public void Test_CombatAmbush_ModifiesDialogueAndEpisodicMemory()
        {
            string npcDir = GetForemanDaxPath();
            var npc = new AgenticNpcInstance(npcDir);
            var now = DateTime.UtcNow;

            npc.RecordCombatInjury(InjuryType.KitinMandibleSlash, InjuryLocation.ChestPlate, 2.5, now);

            Assert.True(npc.Somatic.ActiveInjuries.Count > 0);
            Assert.Contains("COMBAT WOUND", npc.EpisodicMemoryLog[^2]); // Memory entry before the world event
            Assert.Contains("mandible", npc.ActiveAffordance.FormattedDialogue, StringComparison.OrdinalIgnoreCase);
        }
    }
}
