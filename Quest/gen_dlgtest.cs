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
                gen_quest_main._StarfieldMod = env.LoadOrder[0].Mod;

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

                var testNpc = CreateTestNpc();

                // ── Hardcoded DialogueScript (no AI) ──────────────────────────
                var script = new DialogueScript
                {
                    NpcGreeting = "You shouldn't be here. This area is restricted.",
                    Exchanges   = new List<DialogueExchange>
                    {
                        new DialogueExchange
                        {
                            PlayerPrompt = "Who are you?",
                            NpcReply     = "Name's Rook. Facility security.",
                        },
                        new DialogueExchange
                        {
                            PlayerPrompt = "What happened here?",
                            NpcReply     = "Research team went dark three days ago. No distress call, nothing.",
                        },
                        new DialogueExchange
                        {
                            PlayerPrompt = "Is it safe here?",
                            NpcReply     = "Safe enough if you mind your business.",
                        },
                    },
                };

                Console.WriteLine($"[gen_dlgtest] Building NPCDialogueNoun for NPC {testNpc.FormKey}");
                Console.WriteLine($"[gen_dlgtest] Exchanges: {script.Exchanges.Count}");
                Console.WriteLine();

                var noun = new NPCDialogueNoun(
                    npcFormKey:        testNpc.FormKey,
                    voiceTypeEditorId: "testvoice",
                    script:            script,
                    suffix:            "test",
                    elevenLabsVoiceId: "");

                Console.WriteLine();
                PrintDiagnostic(noun.QuestRecord, script);
            }

            foreach (var rec in gen_quest_main.myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

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
        /// </summary>
        private static Npc CreateTestNpc()
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

            return npc;
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

            // Scenes
            Console.WriteLine($"Scenes: {quest.Scenes.Count}  (expected 1)");
            if (quest.Scenes.Count > 0)
            {
                var scene = quest.Scenes[0];
                uint sceneFlags = (uint)(scene.Flags ?? 0);
                string sceneFlagOk = sceneFlags == 0x00001834 ? "OK" : $"MISMATCH (expected 0x00001834)";
                Console.WriteLine($"  [{scene.FormKey}] EditorID={scene.EditorID}");
                Console.WriteLine($"  Flags:   0x{sceneFlags:X8}  [{sceneFlagOk}]");
                Console.WriteLine($"  Actors:  {scene.Actors.Count}  (expected 2)");
                foreach (var a in scene.Actors)
                    Console.WriteLine($"    ID={(int)a.ID} BehaviorFlags={(uint)a.BehaviorFlags} Flags={a.Flags}");
                Console.WriteLine($"  Phases:  {scene.Phases.Count}  (expected 2)");
                foreach (var p in scene.Phases)
                    Console.WriteLine($"    Name=\"{p.Name}\" EditorWidth={p.EditorWidth}");
                Console.WriteLine($"  Actions: {scene.Actions?.Count ?? 0}  (expected 2)");
                if (scene.Actions != null)
                {
                    foreach (var a in scene.Actions)
                    {
                        Console.WriteLine($"    [{a.Index}] {a.GetType().Name} AliasID={a.AliasID} Phase {a.StartPhase}→{a.EndPhase}");
                        if (a is IDialogueSceneActionGetter da)
                            Console.WriteLine($"      Topic: {da.Topic.FormKey}");
                        else if (a is IPlayerDialogueSceneActionGetter pda)
                        {
                            Console.WriteLine($"      DialogueList [{pda.DialogueList.Count}]  (expected {script.Exchanges.Count})");
                            for (int i = 0; i < pda.DialogueList.Count; i++)
                            {
                                var item = pda.DialogueList[i];
                                string pc = item.PlayerChoice.IsNull ? "NULL!" : item.PlayerChoice.FormKey.ToString();
                                string nr = item.NpcResponse.IsNull  ? "NULL!" : item.NpcResponse.FormKey.ToString();
                                Console.WriteLine($"        [{i}] PlayerChoice={pc}  NpcResponse={nr}");
                            }
                        }
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
