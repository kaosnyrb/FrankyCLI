using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.Abstractions;
using Retrograde.Nouns.Hunt;
using System;

namespace FrankyCLI
{
    /// <summary>
    /// Exercises PredatorHuntTarget.GetSystemForPlanet against 10 known planets and
    /// prints the resolved system + recommended level. No mod is written.
    ///
    /// Usage: gen_systemtest
    /// </summary>
    public static class gen_systemtest
    {
        private static readonly string[] Probes =
        {
            "Jemison",          // Alpha Centauri  (1)
            "Akila",            // Cheyenne        (1)
            "Mars",             // Sol             (1)
            "Venus",            // Sol             (1)
            "Gagarin",          // Alpha Centauri  (1)
            "Volii Alpha",      // Volii           (5)
            "Bessel III-b",     // Bessel          (10)
            "Procyon III",      // Procyon A       (10)
            "Verne II",         // Verne           (70)
            "Masada III",       // Masada          (75)
        };

        public static int Run()
        {
            Console.WriteLine("=== gen_systemtest: planet -> system lookup ===");
            Console.WriteLine();

            using var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build();

            gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
            gen_quest_main._StarfieldMod   = env.LoadOrder[0].Mod!;
            gen_quest_main.myMod           = new StarfieldMod(new ModKey("systemtest", ModType.Master), StarfieldRelease.Starfield);

            ModContextImpl.DiscoverTemplateMods(env.LoadOrder, env.DataFolderPath);
            gen_quest_main.BuildReadParams(env.LoadOrder);
            RetrogradeContext.Current = new ModContextImpl();

            int hits = 0, misses = 0;
            Console.WriteLine($"  {"Planet",-18}  {"System",-20}  Level");
            Console.WriteLine($"  {new string('-', 18)}  {new string('-', 20)}  -----");

            foreach (var planet in Probes)
            {
                var result = PredatorHuntTarget.GetSystemForPlanet(planet);
                if (result is null)
                {
                    Console.WriteLine($"  {planet,-18}  {"(not found)",-20}    -");
                    misses++;
                }
                else
                {
                    Console.WriteLine($"  {planet,-18}  {result.Value.system,-20}  {result.Value.level,5}");
                    hits++;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Resolved: {hits}/{Probes.Length}   Missed: {misses}");
            return misses == 0 ? 0 : 1;
        }
    }
}
