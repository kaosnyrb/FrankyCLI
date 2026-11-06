using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// A SPIKE, not a tool: step 0 of office/spikes/2026-11-04-npc-faces-harvest-and-tweak.md, which is its exit
    /// condition (this file goes when that spike graduates or is dropped).
    ///
    /// The question: does an NPC record we write with a CHANGED face show that face in game, or does it need FaceGen
    /// data from the Creation Kit? Vanilla bakes one nif per NPC under
    /// meshes/actors/character/facegendata/facegeom/starfield.esm/, 1,282 of them in "Starfield - FaceMeshes.ba2", and
    /// no record of ours has ever had one.
    ///
    ///   gen_facetest survey                         one TSV row per female HumanRace NPC in Starfield.esm, to stdout
    ///   gen_facetest build [template] [donor] [mod] writes mod.esm (default facetest) with the test's NPCs
    ///
    /// The four: A the template's own face (control); B the donor's four face lists and skin tone on the template;
    /// C is B with lipstick and eyeshadow gone and every ol1 blend raised; D is B again, for a donor nif copied beside it
    /// by hand (his plugin has no route to one: the nif is the question, not this command's job).
    /// HeadParts stay the template's on purpose, so hair and head mesh cannot be mistaken for the face.
    /// </summary>
    public static class gen_facetest
    {
        private const float OL1_RAISE = 0.1f;

        public static int Run(string[] args)
        {
            string verb = args.Length > 1 ? args[1] : "survey";
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();
            var sf = env.LoadOrder[0].Mod!;
            gen_quest_main.BuildReadParams(env.LoadOrder);
            if (verb == "survey") return Survey(sf);
            if (verb == "build")
                return Build(sf, env.DataFolderPath,
                             args.Length > 2 ? args[2] : "UC_NA_MASTWorkerFemale03",
                             args.Length > 3 ? args[3] : throw new ArgumentException("gen_facetest build needs a donor EditorID"),
                             args.Length > 4 ? args[4] : "facetest");
            Console.WriteLine("Usage: gen_facetest survey | build <template> <donor> [modname]");
            return 1;
        }

        private static int Survey(IStarfieldModGetter sf)
        {
            Console.WriteLine("formid\teditorid\tname\tblends\tol1\tmd2\tmd1\tyo1\teu\taf\tas\tdials\ttints\tlipstick\teyeshadow\tskin\tweight_template");
            foreach (var n in sf.Npcs)
            {
                if (!n.Flags.HasFlag(Npc.Flag.Female) || n.MorphBlends == null || n.MorphBlends.Count == 0) continue;
                float Sum(string needle) => n.MorphBlends.Where(b => (b.BlendName ?? "").Contains(needle)).Sum(b => b.Intensity);
                bool Tint(string t) => n.Tints != null && n.Tints.Any(x => (x.TintGroup ?? "").StartsWith(t, StringComparison.OrdinalIgnoreCase));
                Console.WriteLine(string.Join("\t",
                    n.FormKey.ID.ToString("X8"), n.EditorID, n.Name?.String ?? "", n.MorphBlends.Count,
                    Sum("_ol1_").ToString("0.00"), Sum("_md2_").ToString("0.00"), Sum("_md1_").ToString("0.00"), Sum("_yo1_").ToString("0.00"),
                    Sum("_eu_").ToString("0.00"), Sum("_af_").ToString("0.00"), Sum("_as_").ToString("0.00"),
                    n.FaceDialPositions?.Count ?? 0, n.Tints?.Count ?? 0, Tint("Lipstick"), Tint("Eyeshadow"), n.SkinToneIndex,
                    n.DefaultTemplate.IsNull ? "-" : n.DefaultTemplate.FormKey.ToString()));
            }
            return 0;
        }

        private static int Build(IStarfieldModGetter sf, string dataPath, string templateId, string donorId, string modname)
        {
            INpcGetter One(string id) => sf.Npcs.SingleOrDefault(n => n.EditorID == id)
                ?? throw new KeyNotFoundException($"NPC '{id}' is not in Starfield.esm");
            var template = One(templateId);
            var donor = One(donorId);
            if (donor.FormKey == template.FormKey) throw new ArgumentException("donor and template are the same NPC");

            var mod = new StarfieldMod(new ModKey(modname, ModType.Master), StarfieldRelease.Starfield);

            Npc Make(string letter)
            {
                var npc = NPCTools.CloneNPC(mod, template.DeepCopy());
                npc.EditorID = $"duo_facetest_{letter}";
                npc.Name = $"Face Test {letter}";
                npc.Voice.SetTo(template.Voice.FormKey);
                npc.Aggression = Npc.AggressionType.Unaggressive;
                npc.Confidence = Npc.ConfidenceType.Average;
                npc.Factions.Clear();
                npc.CrimeFaction.Clear();
                mod.Npcs.Add(npc);
                return npc;
            }

            void Harvest(Npc npc)
            {
                var d = donor.DeepCopy();
                npc.MorphBlends!.Clear(); npc.MorphBlends.AddRange(d.MorphBlends!);
                npc.FaceMorphs!.Clear(); npc.FaceMorphs.AddRange(d.FaceMorphs!);
                npc.FaceDialPositions!.Clear(); npc.FaceDialPositions.AddRange(d.FaceDialPositions!);
                npc.Tints!.Clear(); npc.Tints.AddRange(d.Tints!);
                // Not in the spike's list, added at build: origin lives in the tints, so a donor of another origin on the
                // template's skin tone would read as "a face that does not match its neck", which is the test's own FAIL.
                npc.SkinToneIndex = d.SkinToneIndex;
            }

            Make("A");
            var b = Make("B"); Harvest(b);
            var c = Make("C"); Harvest(c);
            int removed = c.Tints!.RemoveAll(t => IsMakeup(t.TintGroup));
            var ol1Before = c.MorphBlends!.Where(x => (x.BlendName ?? "").Contains("_ol1_"))
                .Select(x => (region: x.BlendName!.Substring(x.BlendName!.LastIndexOf('_') + 1), value: x.Intensity)).ToList();
            int raised = 0;
            foreach (var blend in c.MorphBlends!.Where(x => (x.BlendName ?? "").Contains("_ol1_")))
            { blend.Intensity += OL1_RAISE; raised++; }
            // FaceMorphs names regions, not presets (Elana: { Index=11, MorphGroup=Neck, 0.1919 } is female_as_ol1_Neck), so a
            // group is matched to its blend by region and value, BEFORE the blends are raised.
            int groups = 0;
            foreach (var (region, value) in ol1Before)
                foreach (var face in c.FaceMorphs!)
                    foreach (var g in face.MorphGroups.Where(x => x.MorphGroup == region && Math.Abs(x.BlendIntensity - value) < 1e-6f))
                    { g.BlendIntensity += OL1_RAISE; groups++; }
            var d2 = Make("D"); Harvest(d2);

            foreach (var rec in mod.EnumerateMajorRecords()) rec.IsCompressed = false;
            string outPath = Path.Combine(dataPath, modname + ".esm");
            mod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());

            Console.WriteLine($"template: {template.EditorID} [{template.FormKey}]  skin {template.SkinToneIndex}");
            Console.WriteLine($"donor:    {donor.EditorID} [{donor.FormKey}] \"{donor.Name?.String}\"  skin {donor.SkinToneIndex}");
            Console.WriteLine($"C: {removed} makeup tints removed, {raised} ol1 blends and {groups} ol1 morph groups raised by {OL1_RAISE}");
            foreach (var n in mod.Npcs)
                Console.WriteLine($"{n.EditorID}\t{n.FormKey.ID:X8}\tUseTemplateActors {n.UseTemplateActors}\tTemplateActors {(n.TemplateActors == null ? "none" : "SET")}\tDefaultTemplate {n.DefaultTemplate.FormKey}");
            Console.WriteLine($"Written: {outPath}");
            return 0;
        }

        private static bool IsMakeup(string? s) =>
            (s ?? "").StartsWith("Lipstick", StringComparison.OrdinalIgnoreCase) || (s ?? "").StartsWith("Eyeshadow", StringComparison.OrdinalIgnoreCase);
    }
}
