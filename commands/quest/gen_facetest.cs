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
        private const string DARK_DERM = "African_Female_ol1_Sk6";
        private const string OLDER_DONOR = "Staryard_GladysCoffin";

        public static int Run(string[] args)
        {
            int rc = Inner(args, out string? written, out string? final);
            // The plugin is in his load order, so the environment holds it open until disposed: a rebuild writes beside it
            // and swaps here, after Inner's environment has gone.
            if (written != null) { File.Move(written, final!, true); Console.WriteLine($"Written: {final}"); }
            return rc;
        }

        private static int Inner(string[] args, out string? written, out string? final)
        {
            written = final = null;
            string verb = args.Length > 1 ? args[1] : "survey";
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();
            var sf = env.LoadOrder[0].Mod!;
            gen_quest_main.BuildReadParams(env.LoadOrder);
            if (verb == "survey") return Survey(sf);
            if (verb == "race")
            {
                foreach (var fd in sf.Races.Single(r => r.EditorID == "HumanRace").ChargenAndSkintones!.Female!.Chargen!.FaceDials)
                    Console.WriteLine("DIAL " + string.Join(" | ", typeof(IFaceDialGetter).GetProperties().Select(q => $"{q.Name}={q.GetValue(fd)}")));
                var race = sf.Races.Single(r => r.EditorID == "HumanRace");
                var fem = race.ChargenAndSkintones!.Female!;
                Console.WriteLine($"FaceSkinTones {fem.FaceSkinTones} | BodySkinTones {fem.BodySkinTones} | HandSkinTones {fem.HandSkinTones} | FCTP {fem.FCTP}");
                foreach (var pr in fem.Chargen!.GetType().GetProperties())
                {
                    var v = pr.GetValue(fem.Chargen);
                    if (v is System.Collections.IEnumerable en && v is not string)
                    {
                        var items = en.Cast<object>().ToList();
                        Console.WriteLine($"{pr.Name} [{items.Count}]");
                        foreach (var it in items.Take(120))
                            Console.WriteLine("  " + string.Join(" | ", it.GetType().GetProperties()
                                .Where(q => q.PropertyType == typeof(string) || q.PropertyType.IsPrimitive)
                                .Select(q => $"{q.Name}={q.GetValue(it)}")));
                    }
                    else Console.WriteLine($"{pr.Name}: {v}");
                }
                return 0;
            }
            if (verb == "build")
                return Build(out written, out final, sf, env.DataFolderPath,
                             args.Length > 2 ? args[2] : "UC_NA_MASTWorkerFemale03",
                             args.Length > 3 ? args[3] : throw new ArgumentException("gen_facetest build needs a donor EditorID"),
                             args.Length > 4 ? args[4] : "facetest");
            Console.WriteLine("Usage: gen_facetest survey | build <template> <donor> [modname]");
            return 1;
        }

        private static int Survey(IStarfieldModGetter sf)
        {
            Console.WriteLine("formid\teditorid\tname\tblends\tol1\tmd2\tmd1\tyo1\teu\taf\tas\tdials\ttints\tlipstick\teyeshadow\tskin\tweight_template\tderm\tderm_intensity\tuse_template\tformversion");
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
                    n.DefaultTemplate.IsNull ? "-" : n.DefaultTemplate.FormKey.ToString(),
                    n.Tints?.FirstOrDefault(x => x.TintGroup == "Dermaesthetic")?.TintName ?? "-",
                    n.Tints?.FirstOrDefault(x => x.TintGroup == "Dermaesthetic")?.TintIntensity.ToString() ?? "-",
                    (ushort)n.UseTemplateActors, n.FormVersion));
            }
            return 0;
        }

        private static int Build(out string? written, out string? final, IStarfieldModGetter sf, string dataPath, string templateId, string donorId, string modname)
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

            void Harvest(Npc npc, INpcGetter? from = null)
            {
                var d = (from ?? donor).DeepCopy();
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

            // E and F, 2026-11-06 am: B reads lighter than Elana, and the guard (skin 6, no bake) reads right through a visor.
            // Same SkinToneIndex; what differs is the Dermaesthetic tint: Elana African_Female_ol1_Sk3 at 31, the guard
            // African_Female_md1_Sk6 at 64. E is B with only that swapped for the Sk6 of her own age. F is the guard's whole
            // face on the same template, no helmet: it separates "the tint" from "the template pulls her light".
            var guard = One("Staryard_UC_GuardF");
            var e = Make("E"); Harvest(e);
            var sk6 = sf.Npcs.SelectMany(n => n.Tints ?? Enumerable.Empty<INpcTintGetter>())
                .FirstOrDefault(t => t.TintName == DARK_DERM)?.DeepCopy()
                ?? throw new KeyNotFoundException($"no NPC in Starfield.esm carries the tint {DARK_DERM}");
            var dermAt = e.Tints!.FindIndex(t => t.TintGroup == "Dermaesthetic");
            if (dermAt < 0) throw new InvalidOperationException("the donor has no Dermaesthetic tint");
            sk6.TintIntensity = 64;
            e.Tints[dermAt] = sk6;
            Console.WriteLine($"E: Dermaesthetic now {sk6.TintName} at {sk6.TintIntensity}, {sk6.TintTexture}");
            var f = Make("F"); Harvest(f, guard);

            // G, same morning: E and F both read light, and the guard turned out to take her traits from a leveled list, so she
            // proved nothing. The one header difference left: ours are written at FormVersion 576, vanilla NPCs at 581, and the
            // engine reads that number to know how to parse the record. G is B with only the header moved.
            var gv = Make("G"); Harvest(gv);
            gv.FormVersion = donor.FormVersion;
            Console.WriteLine($"G: FormVersion {gv.FormVersion}");

            // H, same morning: vanilla's own shape, measured. Of 1,656 female NPCs with face data 1,045 inherit traits and, of the
            // 611 with their own, 591 are baked, so vanilla all but never renders a dark face at runtime. H has the template's
            // own lists, and the Traits flag pointing at the donor, who carries the bake.
            var h = Make("H");
            h.UseTemplateActors = Npc.TemplateActorType.Traits;
            var ta = new TemplateActors();
            ta.TraitTemplate = donor.FormKey.ToNullableLink<INpcTemplateTargetGetter>();
            h.TemplateActors = ta;
            h.Voice.SetTo(donor.Voice.FormKey);

            // I, his "two more alongside": H's route on an older donor, which is Odile's case (OT-06), and with the template's
            // voice LEFT, so talking to her says whether the Traits flag takes the voice, which a voiced Delve person depends on.
            var older = One(OLDER_DONOR);
            var i = Make("I");
            i.UseTemplateActors = Npc.TemplateActorType.Traits;
            var tb = new TemplateActors();
            tb.TraitTemplate = older.FormKey.ToNullableLink<INpcTemplateTargetGetter>();
            i.TemplateActors = tb;

            // J: Elana's own record under a new FormID, every field hers but the FormKey, so the bake is the ONE thing missing.
            // Dark means the template was the fault and the runtime can do it; light means the bake is what carries the colour.
            var j = NPCTools.CloneNPC(mod, donor.DeepCopy());
            j.EditorID = "duo_facetest_J";
            j.Name = "Face Test J";
            j.Voice.SetTo(donor.Voice.FormKey);
            j.Factions.Clear();
            j.CrimeFaction.Clear();
            j.FormVersion = donor.FormVersion;
            mod.Npcs.Add(j);

            foreach (var rec in mod.EnumerateMajorRecords()) rec.IsCompressed = false;
            final = Path.Combine(dataPath, modname + ".esm");
            written = Path.Combine(Path.GetTempPath(), modname + ".esm");
            mod.WriteToBinary(written, gen_quest_main.BuildWriteParams());

            Console.WriteLine($"template: {template.EditorID} [{template.FormKey}]  skin {template.SkinToneIndex}");
            Console.WriteLine($"donor:    {donor.EditorID} [{donor.FormKey}] \"{donor.Name?.String}\"  skin {donor.SkinToneIndex}");
            Console.WriteLine($"C: {removed} makeup tints removed, {raised} ol1 blends and {groups} ol1 morph groups raised by {OL1_RAISE}");
            foreach (var n in mod.Npcs)
                Console.WriteLine($"{n.EditorID}\t{n.FormKey.ID:X8}\tUseTemplateActors {n.UseTemplateActors}\tTemplateActors {(n.TemplateActors == null ? "none" : "SET")}\tDefaultTemplate {n.DefaultTemplate.FormKey}");
            return 0;
        }

        private static bool IsMakeup(string? s) =>
            (s ?? "").StartsWith("Lipstick", StringComparison.OrdinalIgnoreCase) || (s ?? "").StartsWith("Eyeshadow", StringComparison.OrdinalIgnoreCase);
    }
}
