using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.Models;
using Retrograde.Nouns;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// Structural test for NPCDialogueNoun (scene-based pattern).
    ///
    /// Builds a minimal dialogue quest against a cloned NPC, writes the .esm,
    /// then prints a diagnostic summary so the record chain can be verified in xEdit.
    ///
    /// Source of truth: atbb_mq01 [QUST:0008F6] in avontechblacksiteblueprints.esm.
    /// No AI calls, no ElevenLabs — pure Mutagen structure test.
    ///
    /// Usage: gen_dlgtest [modname]
    ///   modname defaults to "dlgtest"
    /// </summary>
    public static class gen_dlgtest
    {
        public static int Run(string modname = "dlgtest")
        {
            string datapath;

            using (var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build())
            {
                gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
                datapath = env.DataFolderPath;
                gen_quest_main._StarfieldMod = env.LoadOrder[0].Mod!;

                ModKey newMod = new ModKey(modname, ModType.Master);
                gen_quest_main.myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);

                ModContextImpl.TemplateModsList = new List<IStarfieldModGetter>();
                for (int i = 0; i < env.LoadOrder.Count; i++)
                {
                    var listing = env.LoadOrder[i];
                    var fileName = listing.FileName.ToString();
                    if (listing.Mod != null &&
                        fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
                        ModContextImpl.TemplateModsList.Add(listing.Mod);
                }

                gen_quest_main.BuildReadParams(env.LoadOrder);
                RetrogradeContext.Current = new ModContextImpl();

                var (testNpc, voiceTypeEditorId) = CreateTestNpc();

                // ── Hardcoded DialogueScript (no AI) ──────────────────────────
                var script = new DialogueScript
                {
                    NpcGreeting = "You shouldn't be here. This area is restricted.",
                    Exchanges   = new List<DialogueExchange>
                    {
                        new DialogueExchange
                        {
                            PlayerPrompt = "Who are you?",
                            NpcReply     = ["Name's Rook. Facility security."],
                        },
                        new DialogueExchange
                        {
                            PlayerPrompt = "What happened here?",
                            NpcReply     = ["Research team went dark three days ago.", "No distress call, nothing."],
                        },
                        new DialogueExchange
                        {
                            PlayerPrompt = "Is it safe here?",
                            NpcReply     = ["Safe enough if you mind your business."],
                        },
                    },
                };

                Console.WriteLine($"[gen_dlgtest] Building NPCDialogueNoun for NPC {testNpc.FormKey}");
                Console.WriteLine($"[gen_dlgtest] Exchanges: {script.Exchanges.Count}");
                Console.WriteLine();

                var noun = new NPCDialogueNoun(
                    npcFormKey:        testNpc.FormKey,
                    voiceTypeEditorId: voiceTypeEditorId,
                    script:            script,
                    suffix:            "test",
                    elevenLabsVoiceId: "ClKfJnuqp0hQ7Ax41F4w");  // Ada (female)

                Console.WriteLine();
                PrintDiagnostic(noun.QuestRecord, script);
                SpeechTools.ConvertAndDeploy();
            }

            foreach (var rec in gen_quest_main.myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            gen_quest_main.PrintNounRegistry();
            string outPath = datapath + "\\" + modname + ".esm";
            gen_quest_main.myMod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());
            Console.WriteLine();
            Console.WriteLine($"[gen_dlgtest] Written: {outPath}");
            Console.WriteLine("[gen_dlgtest] Load in xEdit and verify the record chain manually.");
            return 0;
        }

        // ── NPC factory ───────────────────────────────────────────────────────────

        /// <summary>
        /// Clones MQ101_MinerFemale03 "Miner" [NPC_:00010BE7] from Starfield.esm into the
        /// target mod and applies GagarinFaction [FACT:0015CF54] at rank 0.
        /// Returns the NPC and the EditorID of its VoiceType for use in SpeechTools.
        /// </summary>
        private static (Npc npc, string voiceTypeEditorId) CreateTestNpc()
        {
            var sfKey = gen_quest_main.StarfieldModKey;
            var sfMod = gen_quest_main._StarfieldMod;

            var fk       = new FormKey(sfKey, 0x00010BE7);
            var template = sfMod.Npcs.FirstOrDefault(n => n.FormKey == fk)
                ?? throw new KeyNotFoundException("MQ101_MinerFemale03 [NPC_:00010BE7] not found in Starfield.esm");

            var npc = NPCTools.CloneNPC(gen_quest_main.myMod, template.DeepCopy());
            npc.EditorID = "dlgtest_npc";
            gen_quest_main.myMod.Npcs.Add(npc);

            var gagarinFk = new FormKey(sfKey, 0x0015CF54);
            npc.Factions.Clear();
            npc.Factions.Add(new RankPlacement { Faction = gagarinFk.ToLink<IFactionGetter>(), Rank = 0 });

            var genericFemale01Fk = new FormKey(sfKey, 0x002BCA30);  // GenericFemale01 [VTYP:002BCA30]
            npc.Voice.SetTo(genericFemale01Fk);
            string voiceTypeEditorId = "GenericFemale01";
            Console.WriteLine($"[gen_dlgtest] NPC voice type: {voiceTypeEditorId}");

            return (npc, voiceTypeEditorId);
        }

        // ── Diagnostic printer ─────────────────────────────────────────────────

        private static void PrintDiagnostic(Quest quest, DialogueScript script)
        {
            Console.WriteLine("=== gen_dlgtest diagnostic ===");
            Console.WriteLine();

            // Quest
            uint flags = (uint)(quest.Data?.Flags ?? 0);
            string flagOk = flags == 0x00010111 ? "OK" : $"MISMATCH (expected 0x00010111, got 0x{flags:X8})";
            Console.WriteLine($"Quest:  {quest.EditorID}  ({quest.FormKey})");
            Console.WriteLine($"  Flags:   0x{flags:X8}  [{flagOk}]");
            Console.WriteLine($"  Type:    {quest.Data?.Type}  [expected None]");
            Console.WriteLine($"  Stages:  [{string.Join(", ", quest.Stages.Select(s => s.Index))}]  (expected [0, 100])");
            Console.WriteLine($"  Aliases: {quest.Aliases?.Count ?? 0}  (expected 1)");
            if (quest.Aliases?.Count > 0 && quest.Aliases[0] is QuestReferenceAlias ra)
                Console.WriteLine($"    Alias[0] UniqueActor: {ra.UniqueActor.FormKey}");
            Console.WriteLine();

            // Scenes: 1 greeting (0x1834) + N topic scenes (0x2810 completion + 0x2814 regular)
            int expectedSceneCount = 1 + script.Exchanges.Count;
            Console.WriteLine($"Scenes: {quest.Scenes.Count}  (expected {expectedSceneCount})");
            for (int s = 0; s < quest.Scenes.Count; s++)
            {
                bool isGreeting    = s == 0;
                bool isCompletion  = s == 1;
                uint expectedFlag  = isGreeting ? 0x00001834u : (isCompletion ? 0x00002810u : 0x00002814u);
                int  expPhaseCount = isGreeting ? 1 : 2;
                int  expActionCount = isGreeting ? 1 : 2;
                string label       = isGreeting ? "Greeting" : (isCompletion ? "Completion" : $"Topic[{s-1}]");

                var scene = quest.Scenes[s];
                uint sceneFlags = (uint)(scene.Flags ?? 0);
                string sceneFlagOk = sceneFlags == expectedFlag ? "OK" : $"MISMATCH (expected 0x{expectedFlag:X8})";
                Console.WriteLine($"  [{label}] [{scene.FormKey}] EditorID={scene.EditorID}");
                Console.WriteLine($"    Flags:   0x{sceneFlags:X8}  [{sceneFlagOk}]");
                Console.WriteLine($"    Conditions: {scene.Conditions.Count}  (expected 2)");
                Console.WriteLine($"    Actors:  {scene.Actors.Count}  (expected 2)");
                Console.WriteLine($"    Phases:  {scene.Phases.Count}  (expected {expPhaseCount})");
                foreach (var p in scene.Phases)
                    Console.WriteLine($"      Name=\"{p.Name}\" EditorWidth={p.EditorWidth}");
                Console.WriteLine($"    Actions: {scene.Actions?.Count ?? 0}  (expected {expActionCount})");
                if (scene.Actions != null)
                {
                    foreach (var a in scene.Actions)
                    {
                        Console.WriteLine($"      [{a.Index}] {a.GetType().Name} AliasID={a.AliasID} Phase {a.StartPhase}→{a.EndPhase}");
                        if (a is IDialogueSceneActionGetter da)
                            Console.WriteLine($"        Topic: {da.Topic.FormKey}");
                    }
                }
            }
            Console.WriteLine();

            // Topics
            Console.WriteLine($"DialogTopics: {quest.DialogTopics.Count}  (expected {1 + script.Exchanges.Count * 2})");
            foreach (var t in quest.DialogTopics)
            {
                string catOk = t.Category == DialogTopic.CategoryEnum.Scene ? "OK" : "MISMATCH";
                string subOk = t.Subtype  == DialogTopic.SubtypeEnum.CustomScene ? "OK" : "MISMATCH";
                var info = t.Responses?.FirstOrDefault();
                string text = info?.Responses.Count > 0 ? $"\"{info.Responses[0].ResponseText}\"" : "(empty)";
                uint wem  = info?.Responses.Count > 0 ? info.Responses[0].WEMFile : 0;
                Console.WriteLine($"  [{t.FormKey}] Cat={t.Category}[{catOk}] Sub={t.Subtype}[{subOk}] TPIC={(t.TopicInfoList != null ? "OK" : "NULL!")}");
                Console.WriteLine($"    wem=0x{wem:X8}  text={text}");
            }

            Console.WriteLine();
            Console.WriteLine("=== end diagnostic ===");
        }
    }
}
