using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.Models;
using Retrograde.Nouns;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// Phase-2 structural test for NPCDialogueNoun.
    ///
    /// Builds a minimal 2-stage dialogue quest against a vanilla NPC FormKey,
    /// writes the .esm, then prints a diagnostic summary of the record chain
    /// so the output can be verified in xEdit without running the game.
    ///
    /// No AI calls, no ElevenLabs — pure Mutagen structure test.
    ///
    /// Usage: gen_dlgtest [modname]
    ///   modname defaults to "dlgtest"
    /// </summary>
    public static class gen_dlgtest
    {
        // Male template NPC from Starfield.esm (0x000826)
        private const uint TestNpcId = 0x000826;

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

                var npcFormKey = new FormKey(gen_quest_main.StarfieldModKey, TestNpcId);

                // ── Hardcoded 2-stage DialogueScript (no AI) ─────────────────────
                var script = new DialogueScript
                {
                    Goodbye = "Watch yourself out there.",
                    Stages  = new List<DialogueStage>
                    {
                        new DialogueStage
                        {
                            NpcLine        = "You shouldn't be here. This area is restricted.",
                            ProgressPrompt = "What happened to the research team?",
                            Explores       = new List<DialogueExchange>
                            {
                                new DialogueExchange
                                {
                                    PlayerPrompt = "Who are you?",
                                    NpcReply     = "Name's Rook. Facility security.",
                                },
                                new DialogueExchange
                                {
                                    PlayerPrompt = "Is it safe here?",
                                    NpcReply     = "Safe enough if you mind your business.",
                                },
                            },
                        },
                        new DialogueStage
                        {
                            NpcLine        = "The team disappeared three days ago. No distress call, nothing.",
                            ProgressPrompt = null, // last stage — no progress choice
                            Explores       = new List<DialogueExchange>
                            {
                                new DialogueExchange
                                {
                                    PlayerPrompt = "Any survivors?",
                                    NpcReply     = "One. She's hiding in lab section C.",
                                },
                                new DialogueExchange
                                {
                                    PlayerPrompt = "Who did this?",
                                    NpcReply     = "I don't know. I saw lights in the sky.",
                                },
                            },
                        },
                    },
                };

                Console.WriteLine($"[gen_dlgtest] Building NPCDialogueNoun for NPC {npcFormKey}");
                Console.WriteLine($"[gen_dlgtest] StageCount: {script.StageCount}");
                Console.WriteLine();

                var noun = new NPCDialogueNoun(
                    npcFormKey:        npcFormKey,
                    voiceTypeEditorId: "testvoice",
                    script:            script,
                    suffix:            "test",
                    elevenLabsVoiceId: ""); // no ElevenLabs — structural test only

                Console.WriteLine();
                PrintDiagnostic(noun.QuestRecord, script, npcFormKey);
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

        // ── Diagnostic printer ─────────────────────────────────────────────────

        private static void PrintDiagnostic(Quest quest, DialogueScript script, FormKey npcFormKey)
        {
            Console.WriteLine("=== gen_dlgtest diagnostic ===");
            Console.WriteLine();

            // Quest
            uint flags = (uint)quest.Data.Flags;
            string flagOk = flags == 0x00010111 ? "OK" : $"MISMATCH (expected 0x00010111)";
            Console.WriteLine($"Quest:  {quest.EditorID}  ({quest.FormKey})");
            Console.WriteLine($"  Flags:   0x{flags:X8}  [{flagOk}]");
            Console.WriteLine($"  Stages:  [{string.Join(", ", quest.Stages.Select(s => s.Index))}]  (expected [{string.Join(", ", Enumerable.Range(0, script.StageCount).Select(i => i * 100))}])");

            // Alias
            Console.WriteLine($"  Aliases: {quest.Aliases.Count}  (expected 1)");
            if (quest.Aliases.Count > 0 && quest.Aliases[0] is QuestReferenceAlias ra)
            {
                string actorOk = ra.UniqueActor.FormKey == npcFormKey ? "OK" : "MISMATCH";
                Console.WriteLine($"  Alias[0].UniqueActor: {ra.UniqueActor.FormKey}  [{actorOk}]");
            }

            Console.WriteLine();

            // DialogBranch
            Console.WriteLine($"DialogBranches: {quest.DialogBranches.Count}  (expected 1)");
            if (quest.DialogBranches.Count > 0)
            {
                var b = quest.DialogBranches[0];
                Console.WriteLine($"  [0] {b.EditorID}  Category={b.Category}  Flags={b.Flags}");
                Console.WriteLine($"      StartingTopic: {b.StartingTopic.FormKey}");
            }

            Console.WriteLine();

            // DialogTopics
            Console.WriteLine($"DialogTopics: {quest.DialogTopics.Count}");
            var greetTopic = quest.DialogTopics.FirstOrDefault(t => t.Subtype == DialogTopic.SubtypeEnum.Greeting);
            if (greetTopic != null)
            {
                Console.WriteLine($"  Greeting: {greetTopic.EditorID}  ({greetTopic.FormKey})");
                Console.WriteLine($"    Branch: {greetTopic.Branch.FormKey}");
                Console.WriteLine($"    INFOs:  {greetTopic.Responses.Count}  (expected {script.StageCount}, latest-stage first)");
                for (int i = 0; i < greetTopic.Responses.Count; i++)
                {
                    var info = greetTopic.Responses[i];
                    string cond = info.Conditions.Count > 0 ? "has condition" : "no condition (fallback)";
                    string resp = info.Responses.Count > 0 ? $"\"{info.Responses[0].ResponseText}\"" : "(empty)";
                    uint wem   = info.Responses.Count > 0 ? info.Responses[0].WEMFile : 0;
                    Console.WriteLine($"    [{i}]  {cond}  speaker={info.Speaker.FormKey}  wem=0x{wem:X8}  text={resp}");
                }
            }

            var progTopics  = quest.DialogTopics.Where(t => t.EditorID?.StartsWith("dlg_progress_") == true).ToList();
            var exTopics    = quest.DialogTopics.Where(t => t.EditorID?.StartsWith("dlg_explore_") == true).ToList();
            var byeTopic    = quest.DialogTopics.FirstOrDefault(t => t.Subtype == DialogTopic.SubtypeEnum.Goodbye);

            Console.WriteLine($"  Progress topics: {progTopics.Count}  (expected {script.Stages.Count(s => s.ProgressPrompt != null)})");
            foreach (var pt in progTopics)
            {
                var pi = pt.Responses.FirstOrDefault();
                if (pi?.SetParentQuestStage != null)
                    Console.WriteLine($"    {pt.EditorID}  SetParentQuestStage.OnEnd={pi.SetParentQuestStage.OnEnd}  OnBegin={pi.SetParentQuestStage.OnBegin}");
                else
                    Console.WriteLine($"    {pt.EditorID}  SetParentQuestStage: MISSING");
            }

            int expectedExplores = script.Stages.Sum(s => s.Explores.Count);
            Console.WriteLine($"  Explore topics: {exTopics.Count}  (expected {expectedExplores})");
            foreach (var et in exTopics)
            {
                var ei = et.Responses.FirstOrDefault();
                string resp = ei?.Responses.Count > 0 ? $"\"{ei.Responses[0].ResponseText}\"" : "(empty)";
                uint wem    = ei?.Responses.Count > 0 ? ei.Responses[0].WEMFile : 0;
                Console.WriteLine($"    {et.EditorID}  wem=0x{wem:X8}  text={resp}");
            }

            if (byeTopic != null)
            {
                var bi = byeTopic.Responses.FirstOrDefault();
                string resp = bi?.Responses.Count > 0 ? $"\"{bi.Responses[0].ResponseText}\"" : "(empty)";
                Console.WriteLine($"  Goodbye: {byeTopic.EditorID}  text={resp}");
            }
            else
            {
                Console.WriteLine("  Goodbye: MISSING");
            }

            Console.WriteLine();
            Console.WriteLine("=== end diagnostic ===");
        }
    }
}
