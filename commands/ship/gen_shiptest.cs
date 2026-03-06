using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.Nouns;
using System;
using System.Collections.Generic;
using System.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FrankyCLI
{
    public class gen_shiptest
    {
        private static readonly ISerializer Yaml = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        private static readonly (uint ship, uint faction, string label)[] TestCases =
        {
            (0x00322C13u, 0x000B13A8u, "Spacer_A"),
            (0x0031DF00u, 0x000B1375u, "CrimsonFleet_A"),
            (0x00322BF7u, 0x000AE4F3u, "Ecliptic_A"),
        };

        // Strips Mutagen's read-only wrapper suffix so vanilla and generated type names compare equal.
        static string NormalizeTypeName(string name)
            => name.EndsWith("BinaryOverlay") ? name[..^"BinaryOverlay".Length] : name;

        static Dictionary<string, object?> BuildShipDict(IGenericBaseFormGetter s)
        {
            var d = new Dictionary<string, object?>
            {
                ["editorId"]            = s.EditorID,
                ["template"]            = s.Template.IsNull ? "(null)" : s.Template.FormKey.ToString(),
                ["majorRecordFlags"]    = $"0x{s.MajorRecordFlagsRaw:X8}",
                ["filter"]              = s.Filter ?? "(null)",
                ["dirtinessScale"]      = s.DirtinessScale.ToString(),
                ["navmeshGeometry"]     = s.NavmeshGeometry != null ? "present" : "(null)",
                ["virtualMachineAdapter"] = s.VirtualMachineAdapter == null
                    ? "(null)"
                    : $"{s.VirtualMachineAdapter.Scripts.Count} scripts",
            };

            // ObjectBounds
            d["objectBounds"] = s.ObjectBounds == null ? "(null)" : (object)new Dictionary<string, string>
            {
                ["first"]  = $"{s.ObjectBounds.First.X} {s.ObjectBounds.First.Y} {s.ObjectBounds.First.Z}",
                ["second"] = $"{s.ObjectBounds.Second.X} {s.ObjectBounds.Second.Y} {s.ObjectBounds.Second.Z}",
            };

            // ObjectTemplateInstanceData — hex-encode for byte-exact comparison
            var instDataList = new List<object?>();
            foreach (var entry in s.ObjectTemplateInstanceData ?? [])
            {
                if (entry == null) { instDataList.Add("(null)"); continue; }
                var bytes = new byte[entry.Length];
                for (int i = 0; i < entry.Length; i++) bytes[i] = (byte)entry[i];
                instDataList.Add(new Dictionary<string, object>
                {
                    ["len"] = bytes.Length,
                    ["hex"] = Convert.ToHexString(bytes),
                });
            }
            d["objectTemplateInstanceData"] = instDataList;

            // ObjectTemplates
            var templateList = new List<object?>();
            foreach (var t in s.ObjectTemplates ?? [])
            {
                var tDict = new Dictionary<string, object?>
                {
                    ["name"]       = t.Name?.String ?? "(null)",
                    ["properties"] = t.Properties?.Count ?? 0,
                };
                var includes = new List<object?>();
                foreach (var inc in t.Includes ?? [])
                    includes.Add(new Dictionary<string, object?>
                    {
                        ["mod"]       = inc.Mod.FormKey.ToString(),
                        ["attachIdx"] = inc.AttachPointIndex,
                    });
                tDict["includes"] = includes;
                templateList.Add(tDict);
            }
            d["objectTemplates"] = templateList;

            // Components — split ExternalDataSource sources out from the rest
            var otherComponents = new List<string>();
            var extSources      = new Dictionary<string, string>();
            foreach (var c in s.Components ?? [])
            {
                if (c is IExternalDataSourceComponentGetter edsc)
                {
                    foreach (var src in edsc.Sources ?? [])
                        extSources[src.Name] = src.Source.FormKey.ToString();
                }
                else
                {
                    otherComponents.Add(NormalizeTypeName(c.GetType().Name));
                }
            }
            d["components"] = new Dictionary<string, object?>
            {
                ["count"]               = s.Components?.Count ?? 0,
                ["externalDataSources"] = extSources,
                ["others"]              = otherComponents,
            };

            return d;
        }

        static string ToYaml(IGenericBaseFormGetter s) => Yaml.Serialize(BuildShipDict(s));

        static void DiffYaml(string yamlA, string yamlB, string labelA, string labelB)
        {
            var linesA = yamlA.TrimEnd().Split('\n');
            var linesB = yamlB.TrimEnd().Split('\n');
            int total = Math.Max(linesA.Length, linesB.Length);
            bool anyDiff = false;

            for (int i = 0; i < total; i++)
            {
                string a = i < linesA.Length ? linesA[i] : "(missing)";
                string b = i < linesB.Length ? linesB[i] : "(missing)";
                if (a != b)
                {
                    if (!anyDiff)
                    {
                        Console.WriteLine($"--- {labelA}");
                        Console.WriteLine($"+++ {labelB}");
                        anyDiff = true;
                    }
                    Console.WriteLine($"- {a}");
                    Console.WriteLine($"+ {b}");
                }
            }
            if (!anyDiff)
                Console.WriteLine("  (no differences)");
        }

        public static int Run()
        {
            using var _ = PluginsActivator.ActivateTemplates();
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            gen_quest_main.BuildReadParams(env.LoadOrder);
            gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
            gen_quest_main._StarfieldMod = env.LoadOrder[0].Mod!;

            var testModKey = new ModKey("shiptest", ModType.Master);
            gen_quest_main.myMod = new StarfieldMod(testModKey, StarfieldRelease.Starfield);

            ModContextImpl.TemplateModsList = new List<IStarfieldModGetter>();
            foreach (var listing in env.LoadOrder.ListedOrder)
            {
                var fn = listing.FileName.ToString();
                if (listing.Mod != null &&
                    (fn.Contains("template", StringComparison.OrdinalIgnoreCase)
                     || fn.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))
                    && fn.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
                    ModContextImpl.TemplateModsList.Add(listing.Mod);
            }

            RetrogradeContext.Current = new ModContextImpl();

            var sfMod = gen_quest_main._StarfieldMod;
            var sfKey = gen_quest_main.StarfieldModKey;

            int passed = 0, failed = 0;

            foreach (var (shipId, factionId, label) in TestCases)
            {
                Console.WriteLine();
                Console.WriteLine(new string('=', 60));
                Console.WriteLine($"TEST: {label}  ship=0x{shipId:X8}  faction=0x{factionId:X8}");
                Console.WriteLine(new string('=', 60));

                var vanillaFk = new FormKey(sfKey, shipId);
                var vanilla = sfMod!.GenericBaseForms.FirstOrDefault(r => r.FormKey == vanillaFk);
                if (vanilla == null)
                {
                    Console.WriteLine($"  [ERROR] Vanilla ship 0x{shipId:X8} not found in Starfield.esm");
                    failed++;
                    continue;
                }

                var noun = new SpaceShipNoun("Test " + label, shipId, factionId);

                string vanillaYaml    = ToYaml(vanilla);
                string generatedYaml  = ToYaml(noun.instance);

                Console.WriteLine("\n=== VANILLA ===");
                Console.Write(vanillaYaml);
                Console.WriteLine("\n=== GENERATED ===");
                Console.Write(generatedYaml);
                Console.WriteLine("\n=== DIFF ===");
                DiffYaml(vanillaYaml, generatedYaml, "VANILLA", "GENERATED");

                passed++;
            }

            gen_quest_main.PrintNounRegistry();
            RetrogradeContext.Reset();
            Console.WriteLine();
            Console.WriteLine(new string('=', 60));
            Console.WriteLine($"Results: {passed} tested, {failed} errored");
            return failed > 0 ? 1 : 0;
        }
    }
}
