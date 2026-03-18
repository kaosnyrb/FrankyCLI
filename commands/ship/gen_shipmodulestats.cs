using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// Investigation tool: dumps PropertySheetComponent ActorValues from vanilla ship module GBFMs.
    /// Usage: dummy gen_shipmodulestats
    /// </summary>
    public class gen_shipmodulestats
    {
        public static int Run()
        {
            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            var sfMod = env.LoadOrder[0].Mod!;
            var sfKey = env.LoadOrder[0].ModKey;

            // Track all unique ActorValue FormKeys seen across ship modules
            var avCounts = new Dictionary<FormKey, (string? editorId, int count, List<float> sampleValues, List<string> sampleModules)>();

            // Also track component types seen
            var componentTypeCounts = new Dictionary<string, int>();

            int gbfmCount = 0;
            int shipModuleCount = 0;

            // The FormSpaceshipModule template used by ship modules
            var spaceshipModuleTemplateKey = new FormKey(sfKey, 0x0003058E);

            foreach (var gbfm in sfMod.GenericBaseForms)
            {
                gbfmCount++;

                // Filter to ship modules by template
                if (gbfm.Template.IsNull || gbfm.Template.FormKey != spaceshipModuleTemplateKey)
                    continue;

                shipModuleCount++;

                if (gbfm.Components == null) continue;

                foreach (var component in gbfm.Components)
                {
                    string typeName = component.GetType().Name;
                    if (typeName.EndsWith("BinaryOverlay"))
                        typeName = typeName[..^"BinaryOverlay".Length];

                    if (!componentTypeCounts.ContainsKey(typeName))
                        componentTypeCounts[typeName] = 0;
                    componentTypeCounts[typeName]++;

                    if (component is IPropertySheetComponentGetter propSheet)
                    {
                        if (propSheet.Properties == null) continue;
                        foreach (var prop in propSheet.Properties)
                        {
                            var avKey = prop.ActorValue.FormKey;
                            if (!avCounts.ContainsKey(avKey))
                            {
                                // Try to find EditorID
                                string? avEditorId = null;
                                var avRecord = sfMod.ActorValueInformation.FirstOrDefault(r => r.FormKey == avKey);
                                avEditorId = avRecord?.EditorID;
                                avCounts[avKey] = (avEditorId, 0, new List<float>(), new List<string>());
                            }

                            var entry = avCounts[avKey];
                            entry.count++;
                            if (entry.sampleValues.Count < 10)
                                entry.sampleValues.Add(prop.Value);
                            if (entry.sampleModules.Count < 5)
                                entry.sampleModules.Add(gbfm.EditorID ?? "?");
                            avCounts[avKey] = entry;
                        }
                    }
                }
            }

            Console.WriteLine($"Total GBFMs in Starfield.esm: {gbfmCount}");
            Console.WriteLine($"Ship modules (template=0x0003058E): {shipModuleCount}");
            Console.WriteLine();

            Console.WriteLine("=== Component types on ship modules ===");
            foreach (var kv in componentTypeCounts.OrderByDescending(x => x.Value))
                Console.WriteLine($"  {kv.Key}: {kv.Value}");
            Console.WriteLine();

            Console.WriteLine("=== PropertySheetComponent ActorValues ===");
            Console.WriteLine();
            foreach (var kv in avCounts.OrderByDescending(x => x.Value.count))
            {
                var (editorId, count, sampleValues, sampleModules) = kv.Value;
                var valMin = sampleValues.Count > 0 ? sampleValues.Min() : 0;
                var valMax = sampleValues.Count > 0 ? sampleValues.Max() : 0;
                Console.WriteLine($"  0x{kv.Key.ID:X8}  {editorId ?? "?"}");
                Console.WriteLine($"    Count: {count}  Range: [{valMin} .. {valMax}]");
                Console.WriteLine($"    Values: {string.Join(", ", sampleValues.Distinct().OrderBy(x => x).Take(20))}");
                Console.WriteLine($"    Example modules: {string.Join(", ", sampleModules)}");
                Console.WriteLine();
            }

            return 0;
        }
    }
}
