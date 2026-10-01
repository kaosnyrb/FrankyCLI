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
    // Retune an ENGINE's stats on GenericBaseForms that already exist, FormID-stable.
    //
    //   setengine <modname> <gbfm>[,<gbfm>...] <thrust/pwr> <manoeuvre/pwr> <mass> <health> [power] [--lightweight]
    //
    // WHY IT EXISTS (2026-10-01): the A/B/C ladder restats Stoker 128 and gives every rung an engine
    // sheet, and nothing could move an engine's thrust after `gen_shipstruct --engine` created it.
    // Rebuilding is not neutral (removerecord refuses cells, so a rebuild ORPHANS the part's Cell, and a
    // CK-repointed material swap lives only inside the plugin). Same shape as setshield, setcargo,
    // setmass, for the same reason.
    //
    // UNITS, because they are the trap on this class: thrust and manoeuvre are PER POWER (the field you
    // type is the design number; the builder shows it x power). Mass and health are ABSOLUTE per module.
    // Manual part 01, proven at the glass 2026-07-23.
    //
    // WHAT IT WRITES: SpaceshipEnginePartForce, SpaceshipThrusterPartForce, SpaceshipPartMass, and
    // ShipSystemEngineHealth == ShipSystemEngineEMHealth from ONE value (equal on every vanilla engine;
    // two parameters would be two ways to make them disagree). Optionally SpaceshipEnginePartMaxPower.
    // Top speed, strafe, boost and crew are the class's and are NOT touched.
    //
    // THE GRADE, read off the part's OWN ShipModuleClass keyword, never asked for:
    //   * thrust/pwr and manoeuvre/pwr against the class CEILING (the ladder's elite ties it, never
    //     crosses it) -- a refusal;
    //   * thrust per unit of mass-per-power against the class MAXIMUM. Mass is the brake: under-massing
    //     an engine is a stealth power increase (his 2026-07-23 "light engines fuck up the mass/thrust
    //     ratios", and the Shipyards warp trio's 8x). Refused by default.
    //     `--lightweight` widens that one limit by exactly 1/0.85, because the Avontech signature (his
    //     2026-10-01 ruling) is engines at 85% of vanilla mass and it deliberately takes Stoker 290
    //     past class B's best ratio. An accidental light engine is still refused; the deliberate one
    //     passes only when somebody names it.
    // Ceilings measured off every class-A/B/C engine recipe in Starfield.esm by ladder_bands.py
    // (Stardust repo), 2026-10-01; the quest-only SAL-6830 is excluded, as the manual excludes it.
    //
    // Validates every target before mutating anything; idempotent; refuses an engine sheet onto a
    // record that has none.
    class gen_setengine
    {
        const uint AV_ENGINE_FORCE = 0x00ACDC;
        const uint AV_ENGINE_MAX_POWER = 0x00ACDD;
        const uint AV_THRUSTER_FORCE = 0x00ACDE;
        const uint AV_PART_MASS = 0x00ACDB;
        const uint AV_ENGINE_HEALTH = 0x1EF0CD;
        const uint AV_ENGINE_EM_HEALTH = 0x1EF0C2;
        const float LIGHTWEIGHT = 0.85f;

        static readonly Dictionary<uint, string> ClassKeywords = new()
        {
            { 0x0026FE57, "A" },
            { 0x0026FE56, "B" },
            { 0x0026FE55, "C" },
        };

        // (thrust/pwr ceiling, manoeuvre/pwr ceiling, max thrust per unit of mass-per-power)
        static readonly Dictionary<string, (float thrust, float man, float ratio)> Ceiling = new()
        {
            { "A", (7700f, 1610f, 235.8f) },   // Amun-7 · SA-4330 · White Dwarf 3030
            { "B", (8860f, 1850f, 184.6f) },   // Dunn-71 · SAE-5660 · Dunn-71
            { "C", (9000f, 3900f, 103.4f) },   // SAL-6330 · SAL-6330 · Poseidon DT230
        };

        public static int Generate(string[] args)
        {
            bool lightweight = args.Contains("--lightweight");
            var a = args.Where(x => x != "--lightweight").ToArray();
            if (a.Length < 7)
            {
                Console.WriteLine("Usage: setengine <modname> <gbfm>[,<gbfm>...] <thrust/pwr> <manoeuvre/pwr> <mass> <health> [power] [--lightweight]");
                Console.WriteLine("Thrust and manoeuvre are PER POWER; mass and health are per module.");
                return 1;
            }
            string modname = a[0];
            var targets = a[2].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToList();
            var vals = new float[4];
            string[] names = { "thrust/pwr", "manoeuvre/pwr", "mass", "health" };
            for (int i = 0; i < 4; i++)
            {
                if (!float.TryParse(a[3 + i], out vals[i]) || vals[i] <= 0)
                {
                    Console.WriteLine($"Error: {names[i]} must be a positive number (got '{a[3 + i]}')");
                    return 1;
                }
            }
            float thrust = vals[0], man = vals[1], mass = vals[2], health = vals[3];
            float power = -1;
            if (a.Length >= 8)
            {
                if (!float.TryParse(a[7], out power) || power < 1 || power > 12 || power != Math.Floor(power))
                {
                    Console.WriteLine($"Error: power must be a whole number 1..12 (got '{a[7]}')");
                    return 1;
                }
            }
            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                datapath = env.DataFolderPath;
                ModKey modKey = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(modKey))
                {
                    Console.WriteLine($"Error: {modname}.esm is not in the load order");
                    return 1;
                }
                ModPath modPath = System.IO.Path.Combine(datapath, modname + ".esm");
                myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                gen_quest_main.FixNextFormId(myMod);
                var sfKey = env.LoadOrder[0].ModKey;

                var found = new List<IGenericBaseFormGetter>();
                foreach (var target in targets)
                {
                    var existing = myMod.GenericBaseForms.FirstOrDefault(
                        g => string.Equals(g.EditorID, target, StringComparison.OrdinalIgnoreCase));
                    if (existing == null)
                    {
                        Console.WriteLine($"Error: no GenericBaseForm '{target}' in {modname}");
                        return 1;
                    }
                    var sheet = existing.Components?.OfType<PropertySheetComponent>().FirstOrDefault();
                    var curPower = sheet?.Properties.FirstOrDefault(p => p.ActorValue.FormKey == new FormKey(sfKey, AV_ENGINE_MAX_POWER));
                    if (sheet == null || !sheet.Properties.Any(p => p.ActorValue.FormKey == new FormKey(sfKey, AV_ENGINE_FORCE)) || curPower == null)
                    {
                        Console.WriteLine($"Error: {existing.EditorID} has no engine sheet -- refusing to author one"
                            + " onto it (a decorative twin carries mass only, on purpose).");
                        return 1;
                    }
                    string? cls = null;
                    foreach (var kw in existing.Components?.OfType<KeywordFormComponent>().FirstOrDefault()?.Keywords
                                       ?? Enumerable.Empty<IFormLinkGetter<IKeywordGetter>>())
                    {
                        if (kw.FormKey.ModKey == sfKey && ClassKeywords.TryGetValue(kw.FormKey.ID, out var c)) { cls = c; break; }
                    }
                    if (cls == null)
                    {
                        Console.WriteLine($"Error: {existing.EditorID} carries no ShipModuleClass<A|B|C> keyword, so its"
                            + " ceiling cannot be read off the record. Add one with `setkeyword` first.");
                        return 1;
                    }
                    var cap = Ceiling[cls];
                    float pw = power > 0 ? power : curPower.Value;
                    float ratio = thrust / (mass / pw);
                    float ratioCap = lightweight ? cap.ratio / LIGHTWEIGHT : cap.ratio;
                    bool over = false;
                    if (thrust > cap.thrust)
                    {
                        Console.WriteLine($"REFUSED ({existing.EditorID}): thrust/pwr {thrust} exceeds the class-{cls} ceiling {cap.thrust}.");
                        over = true;
                    }
                    if (man > cap.man)
                    {
                        Console.WriteLine($"REFUSED ({existing.EditorID}): manoeuvre/pwr {man} exceeds the class-{cls} ceiling {cap.man}.");
                        over = true;
                    }
                    if (ratio > ratioCap + 0.05f)
                    {
                        Console.WriteLine($"REFUSED ({existing.EditorID}): thrust per mass-per-power {ratio:0.0} exceeds the class-{cls}"
                            + $" maximum {ratioCap:0.0}" + (lightweight ? " even with --lightweight." :
                              ". Mass is the brake. If this is the Avontech lightweight trait, say so with --lightweight."));
                        over = true;
                    }
                    if (over) return 1;
                    Console.WriteLine($"  {existing.EditorID}: class {cls}, {thrust}/{man} per power at mass {mass}, power {pw}"
                        + $" (thrust/mass-per-power {ratio:0.0}, class max {cap.ratio:0.0}{(lightweight ? $", lightweight limit {ratioCap:0.0}" : "")})");
                    found.Add(existing);
                }

                foreach (var existing in found)
                {
                    var gbfm = existing.DeepCopy();
                    var sheet = gbfm.Components.OfType<PropertySheetComponent>().First();
                    var writes = new List<(uint av, float value, string label)>
                    {
                        (AV_ENGINE_FORCE, thrust, "SpaceshipEnginePartForce"),
                        (AV_THRUSTER_FORCE, man, "SpaceshipThrusterPartForce"),
                        (AV_PART_MASS, mass, "SpaceshipPartMass"),
                        (AV_ENGINE_HEALTH, health, "ShipSystemEngineHealth"),
                        (AV_ENGINE_EM_HEALTH, health, "ShipSystemEngineEMHealth"),
                    };
                    if (power > 0) writes.Add((AV_ENGINE_MAX_POWER, power, "SpaceshipEnginePartMaxPower"));
                    bool touched = false;
                    foreach (var (av, value, label) in writes)
                    {
                        var key = new FormKey(sfKey, av);
                        var prop = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == key);
                        if (prop == null)
                        {
                            Console.WriteLine($"Error: {gbfm.EditorID} lacks {label}; an engine sheet missing it is not one this writes to");
                            return 1;
                        }
                        if (Math.Abs(prop.Value - value) < 0.0001f)
                        {
                            Console.WriteLine($"  {gbfm.EditorID}: {label} already {value} -- left as is");
                            continue;
                        }
                        Console.WriteLine($"  {gbfm.EditorID}: {label} {prop.Value} -> {value}");
                        prop.Value = value;
                        touched = true;
                    }
                    if (!touched) continue;
                    myMod.GenericBaseForms.Remove(existing.FormKey);
                    myMod.GenericBaseForms.Add(gbfm);
                    changed++;
                }
            }
            if (changed == 0)
            {
                Console.WriteLine("Nothing to write.");
                return 0;
            }
            foreach (var rec in myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine($"Finished -- {changed} GenericBaseForm(s) patched, FormIDs unchanged.");
            return 0;
        }
    }
}
