using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// `gen_delve faces [--dry]`: write data/delves/faces.json and faces.md, every vanilla human whose face can be worn by a
        /// Delve person, read off Starfield.esm only. His 2026-11-04 "I dont want to have to sit and make all the faces by
        /// hand", and the 2026-11-06 face test, six plays in game (office/spikes/2026-11-04-npc-faces-harvest-and-tweak.md).
        ///
        /// WHAT THE TEST SETTLED, and why this table is the shape it is. A cloned NPC takes face SHAPE and makeup from its record
        /// at runtime, at ONE face tone whatever the record says; SkinToneIndex moves the hands and body only, his "Skin and
        /// face colour are seperate". The tone of a face is in its BAKE: one nif in Starfield - FaceMeshes.ba2 and four
        /// textures in Starfield - Textures01.ba2, keyed on the FormID of the NPC. A copied bake renders broken. What
        /// works is vanilla's own shape: a person points at a baked NPC through TemplateActors.TraitTemplate, and wears that
        /// NPC's whole head: face, skin, hair, jewelry. So a row here is a NPC with her own traits AND a bake.
        ///
        /// `skin` is her SkinToneIndex, and on a BAKED NPC it is the tone of the face: ten sampled off their _color textures run
        /// 0 about 190 in brightness, 2 about 166, 5 about 150, 6 about 118. `origin` is NOT colour: it is the game's bone family
        /// (off her Dermaesthetic tint, else her heaviest blends), and plenty of `af` faces are skin 0. His race-mixing point
        /// is met by reading `skin` and `origin` together. `unique` marks somebody in the game: a player might know that face,
        /// so a writer prefers a generic one.
        /// ⛔ THE TRAITS FLAG TAKES THE VOICE TOO, his MB41a play 2026-11-06, "face looks right, but no audio": Odile wore a head
        /// with no VoiceType, so her lines, filed under GenericFemale01, were looked for under nothing. So `voice` is in the
        /// table, and a voiced person's recipe `voice` must equal her donor's, refused at build otherwise.
        /// Vanilla only, deliberately, like fauna.json. Generated: never hand-edit faces.json, re-run this.
        /// </summary>
        private static int Faces(string dataDir, bool dry)
        {
            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            var sfKey = ModKey.FromNameAndExtension("Starfield.esm");
            var sf = env.LoadOrder.PriorityOrder.FirstOrDefault(l => l.ModKey == sfKey)?.Mod;
            if (sf == null) { Console.WriteLine("REFUSED: Starfield.esm is not in the load order."); return 1; }

            var nifs = ArchiveNames(Path.Combine(env.DataFolderPath, "Starfield - FaceMeshes.ba2"),
                                    "meshes/actors/character/facegendata/facegeom/starfield.esm/", ".nif");
            var colours = ArchiveNames(Path.Combine(env.DataFolderPath, "Starfield - Textures01.ba2"),
                                       "textures/actors/character/facecustomization/starfield.esm/", "_color.dds");
            Console.WriteLine($"  baked: {nifs.Count} face nifs, {colours.Count} face colour textures");

            var parts = sf.HeadParts.ToDictionary(h => h.FormKey, h => h.EditorID ?? "");
            var voices = sf.VoiceTypes.ToDictionary(v => v.FormKey, v => v.EditorID ?? "");
            var human = new FormKey(sfKey, 0x00347D);
            var skip = new Regex("corpse|dead|test|audio|noappearance|preset|mannequin|ghost|clone", RegexOptions.IgnoreCase);
            var rows = new List<FaceRow>();
            int ownFemale = 0, excluded = 0, untagged = 0, otherRace = 0, noColour = 0;
            foreach (var n in sf.Npcs)
            {
                string id = n.FormKey.ID.ToString("x8");
                if (n.UseTemplateActors.HasFlag(Npc.TemplateActorType.Traits) || !nifs.Contains(id)) continue;
                if (n.Flags.HasFlag(Npc.Flag.Female) && (n.MorphBlends?.Count ?? 0) > 0) ownFemale++;
                if (n.Race.FormKey != human) { otherRace++; continue; }
                if (!colours.Contains(id)) { noColour++; continue; }
                if (skip.IsMatch(n.EditorID ?? "")) { excluded++; continue; }
                var (age, origin) = AgeAndOrigin(n);
                if (age == null || origin == null) { untagged++; continue; }
                string? Part(string needle) => n.HeadParts.Select(h => parts.GetValueOrDefault(h.FormKey, ""))
                    .FirstOrDefault(p => p.Contains(needle, StringComparison.OrdinalIgnoreCase));
                bool Tint(string g) => n.Tints?.Any(t => (t.TintGroup ?? "").StartsWith(g, StringComparison.OrdinalIgnoreCase)) == true;
                var marks = new List<string>();
                if (Tint("Scars")) marks.Add("scars");
                if (Tint("TattooMask")) marks.Add("tattoo");
                if (Tint("MakeupFullPaint")) marks.Add("paint");
                if (Tint("EnvironmentMask")) marks.Add("dirt");
                if (Tint("Lipstick") || Tint("Eyeshadow")) marks.Add("makeup");
                rows.Add(new FaceRow
                {
                    editorId = n.EditorID!,
                    formKey = n.FormKey.ToString(),
                    name = string.IsNullOrWhiteSpace(n.Name?.String) ? null : n.Name!.String,
                    sex = n.Flags.HasFlag(Npc.Flag.Female) ? "female" : "male",
                    age = age,
                    origin = origin,
                    skin = n.SkinToneIndex ?? 0,
                    voice = n.Voice.IsNull ? null : voices.GetValueOrDefault(n.Voice.FormKey),
                    hair = Short(Part("_Hair_")),
                    beard = Short(Part("Beard")),
                    marks = marks,
                    unique = n.Flags.HasFlag(Npc.Flag.Unique),
                });
            }
            rows = rows.OrderBy(r => r.sex).ThenBy(r => r.age).ThenBy(r => r.origin).ThenBy(r => r.skin).ThenBy(r => r.editorId).ToList();
            Console.WriteLine($"  faces: {rows.Count} ({rows.Count(r => r.sex == "female")} female, {rows.Count(r => r.sex == "male")} male, "
                              + $"{rows.Count(r => !r.unique)} generic); {excluded} skipped by name, {untagged} with no age or origin read, {otherRace} other race, {noColour} a nif and no colour texture");

            // THE CONTROLS, asserted before anything is written. Each is a fact from the face test, read independently.
            int bad = 0;
            void Want(string what, bool ok) { Console.WriteLine($"  control  : {what} {(ok ? "OK" : "FAIL")}"); if (!ok) bad++; }
            var elana = rows.FirstOrDefault(r => r.editorId == "HT_ElanaNwankwo");
            Want("Elana Nwankwo is female, af, ol1, skin 6", elana is { sex: "female", origin: "af", age: "ol1", skin: 6 });
            var gladys = rows.FirstOrDefault(r => r.editorId == "Staryard_GladysCoffin");
            Want("Gladys Coffin is female, eu, ol1", gladys is { sex: "female", origin: "eu", age: "ol1" });
            Want("the UC guard (traits from a list) is absent", rows.All(r => r.editorId != "Staryard_UC_GuardF"));
            Want($"591 female NPCs have their own traits and a baked nif, the survey's count (read {ownFemale})", ownFemale == 591);
            if (bad > 0) { Console.WriteLine($"REFUSED: {bad} control(s) failed; nothing written."); return 1; }

            string path = Path.Combine(dataDir, "faces.json");
            string json = JsonSerializer.Serialize(new FaceTable { faces = rows }, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
            string mdPath = Path.Combine(dataDir, "faces.md");
            string md = FacesMarkdown(rows);
            if (dry) { Console.WriteLine($"  --dry: would write {path} ({json.Length:N0} chars) and {mdPath} ({md.Length:N0} chars)"); return 0; }
            File.WriteAllText(path, json + "\n");
            File.WriteAllText(mdPath, md);
            Console.WriteLine($"  wrote {path} ({json.Length:N0} chars) and {mdPath} ({md.Length:N0} chars)");
            return 0;
        }

        /// <summary>This NPC's row in faces.json, or null. A recipe's person.face is refused off the table: on any other NPC it
        /// does not do what it says.</summary>
        private static FaceRow? FaceOf(string editorId)
        {
            string? dataDir = ResolveDataDir();
            if (dataDir == null) return null;
            string path = Path.Combine(dataDir, "faces.json");
            if (!File.Exists(path)) return null;
            return JsonSerializer.Deserialize<FaceTable>(File.ReadAllText(path))?.faces.FirstOrDefault(f => f.editorId == editorId);
        }

        private static (string? age, string? origin) AgeAndOrigin(INpcGetter n)
        {
            string? derm = n.Tints?.FirstOrDefault(t => t.TintGroup == "Dermaesthetic")?.TintName;
            if (derm != null)
            {
                var m = Regex.Match(derm, "(african|asian|european|af|as|eu)_(?:female_|male_)?(yo1|md1|md2|ol1)", RegexOptions.IgnoreCase);
                var m2 = m.Success ? m : Regex.Match(derm, "(?:fe)?male_(af|as|eu)_(yo1|md1|md2|ol1)", RegexOptions.IgnoreCase);
                var m3 = m2.Success ? m2 : Regex.Match(derm, "(African|Asian|European)_(?:Fe)?[Mm]ale_(yo1|md1|md2|ol1)", RegexOptions.IgnoreCase);
                if (m3.Success) return (m3.Groups[2].Value.ToLowerInvariant(), m3.Groups[1].Value.ToLowerInvariant().Substring(0, 2) switch { "ei" => "eu", var x => x });
            }
            var sums = new Dictionary<string, float>();
            foreach (var b in n.MorphBlends ?? Enumerable.Empty<INpcMorphBlendGetter>())
            {
                var m = Regex.Match(b.BlendName ?? "", "_(af|as|eu)_(yo1|md1|md2|ol1)_", RegexOptions.IgnoreCase);
                if (!m.Success) continue;
                foreach (var k in new[] { m.Groups[1].Value.ToLowerInvariant(), m.Groups[2].Value.ToLowerInvariant() })
                    sums[k] = sums.GetValueOrDefault(k) + b.Intensity;
            }
            string? Top(params string[] keys) => keys.Where(sums.ContainsKey).OrderByDescending(k => sums[k]).FirstOrDefault();
            return (Top("yo1", "md1", "md2", "ol1"), Top("af", "as", "eu"));
        }

        private static string? Short(string? part)
        {
            if (string.IsNullOrEmpty(part)) return null;
            var s = Regex.Replace(part, "^Human_(Fe)?[Mm]ale_(Hair_|Beard_)?", "");
            return Regex.Replace(s, "_(Back|Front|Top|Bottom)$", "");
        }

        /// <summary>The lower-cased file stems under one folder of a BTDX archive, from its name table.</summary>
        private static HashSet<string> ArchiveNames(string ba2, string folder, string suffix)
        {
            var names = new HashSet<string>();
            using var r = new BinaryReader(File.OpenRead(ba2));
            if (new string(r.ReadChars(4)) != "BTDX") throw new InvalidDataException($"{ba2}: not a BTDX archive");
            r.BaseStream.Seek(12, SeekOrigin.Begin);
            uint count = r.ReadUInt32();
            r.BaseStream.Seek((long)r.ReadUInt64(), SeekOrigin.Begin);
            for (uint i = 0; i < count; i++)
            {
                var s = Encoding.Latin1.GetString(r.ReadBytes(r.ReadUInt16())).Replace('\\', '/').ToLowerInvariant();
                if (s.StartsWith(folder) && s.EndsWith(suffix)) names.Add(s.Substring(folder.Length, s.Length - folder.Length - suffix.Length));
            }
            return names;
        }

        private static string FacesMarkdown(List<FaceRow> rows)
        {
            var sb = new StringBuilder();
            sb.Append("# Faces a Delve person can wear\n\n");
            sb.Append("*Generated by `gen_delve faces` off Starfield.esm. Never hand-edit; re-run it.*\n\n");
            sb.Append("Set a person's `face` to an EditorID below and she wears that NPC's whole head: face, skin, hair and jewelry. ");
            sb.Append("Name and outfit stay the recipe's. A voiced person's `voice` must be the one in her row, because the face brings its voice type with it; a row with `none` cannot speak. Prefer a row with no ★: a ★ is somebody in the game, so a player may know the face.\n\n");
            sb.Append("`age`: yo1 young, md1 middle, md2 later middle, ol1 old. `skin`: 0 lightest to 8 darkest, the tone of this face. ");
            sb.Append("`origin`: af, as, eu, the game's bone families, NOT a colour: many `af` faces are skin 0, so read the two together.\n");
            foreach (var sex in rows.GroupBy(r => r.sex))
                foreach (var age in sex.GroupBy(r => r.age))
                {
                    sb.Append($"\n## {sex.Key}, {age.Key} ({age.Count()})\n\n| EditorID | name | origin | skin | voice | hair | marks |\n|---|---|---|---|---|---|---|\n");
                    foreach (var r in age)
                        sb.Append($"| `{r.editorId}` | {(r.unique ? "★ " : "")}{r.name ?? ""} | {r.origin} | {r.skin} | {r.voice ?? "none"} | {r.hair ?? ""} | {string.Join(", ", r.marks)} |\n");
                }
            return sb.ToString();
        }

        private sealed class FaceTable { public List<FaceRow> faces { get; set; } = new(); }
        private sealed class FaceRow
        {
            public string editorId { get; set; } = "";
            public string formKey { get; set; } = "";
            public string? name { get; set; }
            public string sex { get; set; } = "";
            public string age { get; set; } = "";
            public string origin { get; set; } = "";
            public int skin { get; set; }
            public string? voice { get; set; }
            public string? hair { get; set; }
            public string? beard { get; set; }
            public List<string> marks { get; set; } = new();
            public bool unique { get; set; }
        }
    }
}
