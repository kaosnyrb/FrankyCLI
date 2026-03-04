using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Lore;
using Retrograde.Nouns;
using Retrograde.Quests.TemplateEngines;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI;

/// <summary>
/// Generates an ESM from a lore mission folder.
///
/// Usage:
///   dotnet run -- gen_lore lore/bad_batch [modname]
///
/// Pipeline:
///   LoreLoader.Load(missionFolder)
///   → Create NPC Mutagen records (NpcLoreNoun) + register in LoreRegistry
///   → Ambient dialogue (BranchingDialogueNoun) per NPC with ambient_dialogue
///   → Quest chain: clone template → apply lore overrides → IOutlawQuest.Setup()
///   → WriteToBinary
/// </summary>
public class gen_lore_main
{
    public static int Run(string missionFolder, string modname)
    {
        Console.WriteLine($"gen_lore: loading mission from '{missionFolder}'");

        using var _ = PluginsActivator.ActivateTemplates();

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

            // Discover template mods
            ModContextImpl.TemplateModsList = new List<IStarfieldModGetter>();
            foreach (var listing in env.LoadOrder.ListedOrder)
            {
                var fileName = listing.FileName.ToString();
                if (listing.Mod != null &&
                    (fileName.Contains("template", StringComparison.OrdinalIgnoreCase)
                     || fileName.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))
                    && fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
                {
                    ModContextImpl.TemplateModsList.Add(listing.Mod);
                    Console.WriteLine($"Template mod: {listing.FileName}");
                }
            }

            // Populate MasterFlagsCache so BuildWriteParams() works (no existing mod to load from)
            gen_quest_main.BuildReadParams(env.LoadOrder);

            RetrogradeContext.Current = new ModContextImpl();
            LoreRegistry.Clear();

            AITools.AIMODE = true;
            SpeechTools.generateWavs = true;  // generate WAVs for lore NPC dialogue

            // ── 1. Load mission ──────────────────────────────────────────────────
            var loaded = LoreLoader.Load(missionFolder);
            Console.WriteLine($"Mission loaded: {loaded.Mission.Name} ({loaded.Quests.Count} quests, {loaded.Npcs.Count} NPCs)");

            // ── 2. Create NPC records ────────────────────────────────────────────
            var loreNpcs = new Dictionary<string, OutlawNpc>();
            foreach (var npcLore in loaded.Npcs)
            {
                var noun = new NpcLoreNoun(npcLore);
                loreNpcs[npcLore.Id] = noun.Result;
            }

            // Procedural fallback NPC for quests that don't name a boss
            var fallbackNpc = new OutlawNpc(gen_quest_main.myMod, false);
            fallbackNpc.GenerateNPC();

            // ── 3. Ambient dialogue ──────────────────────────────────────────────
            foreach (var npcLore in loaded.Npcs.Where(n => !string.IsNullOrEmpty(n.AmbientDialogue)))
            {
                if (!loaded.Dialogues.TryGetValue(npcLore.AmbientDialogue, out var dlg))
                {
                    Console.WriteLine($"Warning: dialogue '{npcLore.AmbientDialogue}' for NPC '{npcLore.Id}' not loaded.");
                    continue;
                }
                var npcFormKey = loreNpcs[npcLore.Id].instance.FormKey;
                new BranchingDialogueNoun(npcFormKey, npcLore.VoiceType, dlg, npcLore.ElevenLabsVoiceId ?? "");
                Console.WriteLine($"  Branching dialogue built for: {npcLore.Name}");
            }

            // ── 4. Quest chain ───────────────────────────────────────────────────
            // Build a flat template index from all Templates_* classes
            var templateManager = new AllTemplateManager(new AI_TemplateEngine());
            var allTemplates = templateManager.CompleteLib.DiscoveryTemplates
                .Concat(templateManager.CompleteLib.InvestigationTemplates)
                .Concat(templateManager.CompleteLib.ShowdownTemplates)
                .ToList();

            // Chain quests in REVERSE so each quest knows its nextQuest link
            IOutlawQuest? previousOutlawQuest = null;
            foreach (var questLore in Enumerable.Reverse(loaded.Quests))
            {
                var template = allTemplates.FirstOrDefault(t => t.Name == questLore.Template);
                if (template == null)
                {
                    Console.WriteLine($"Warning: template '{questLore.Template}' not found — skipping quest '{questLore.Id}'.");
                    continue;
                }

                // Apply lore overrides onto template (bypass will set PromptManager.ActiveLore)
                template.Lore = questLore;

                // Merge YAML parameters (YAML wins on conflict)
                if (questLore.Parameters?.Count > 0)
                {
                    template.parameters ??= new Dictionary<string, object>();
                    foreach (var kv in questLore.Parameters)
                        template.parameters[kv.Key] = kv.Value;
                }

                // Merge addons
                if (questLore.Addons?.Count > 0)
                {
                    template.Addons ??= new List<string>();
                    template.Addons.AddRange(questLore.Addons);
                }

                // Resolve named boss NPC
                OutlawNpc questNpc = fallbackNpc;
                if (questLore.Npcs?.TryGetValue("boss", out var bossId) == true
                    && !string.IsNullOrEmpty(bossId)
                    && loreNpcs.TryGetValue(bossId, out var namedBoss))
                {
                    questNpc = namedBoss;
                }

                Console.WriteLine($"  Quest: {questLore.Id} ({template.Name})");
                template.outlawQuest.Setup(gen_quest_main.myMod, questNpc, template, previousOutlawQuest);
                previousOutlawQuest = template.outlawQuest;

                // Clear PromptManager bypass for next iteration
                PromptManager.ActiveLore = null;
            }
        }

        // ── 5. Decompress + write ────────────────────────────────────────────────
        foreach (var rec in gen_quest_main.myMod.EnumerateMajorRecords())
            rec.IsCompressed = false;

        var outputPath = Path.Combine(datapath, modname + ".esm");
        gen_quest_main.myMod.WriteToBinary(outputPath, gen_quest_main.BuildWriteParams());
        SpeechTools.ConvertAndDeploy();
        Console.WriteLine($"gen_lore: wrote {outputPath}");
        return 0;
    }
}
