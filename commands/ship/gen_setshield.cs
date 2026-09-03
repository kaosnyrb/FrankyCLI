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
    // Retune a SHIELD generator's stats on GenericBaseForms that already exist, FormID-stable.
    //
    //   setshield <modname> <gbfm_editorid>[,<gbfm_editorid>...] <health> <mass> [power] [syshealth]
    //
    // WHY BOTH THIS AND `struct --shield`, which is the same question setcargo answered and the
    // same answer: a flag only ever reaches a part at CREATION, and shield health is exactly the
    // number an author re-tunes after seeing the part on a ship. Rebuilding to change it is NOT
    // neutral -- removerecord refuses cells by design, so regenerating ORPHANS the part's Cell,
    // and a CK-repointed LayeredMaterialSwap lives only inside the plugin with no source to
    // rebuild from. Same shape as setmass, setcargo, setfuel, setobnd and setlightlayer, for the
    // same reason each time.
    //
    // ⭐ AND WHY IT WRITES FOUR PROPERTIES RATHER THAN ONE: health and mass are a PAIR. Health
    // alone is not a balance decision -- a shield that is light for its health is a stealth buff
    // to the whole ship, and a health-only setter would let someone move one half of the pair and
    // leave the graded ratio behind. That is the Shipyards engine audit's mistake made available
    // as a command. So mass is required, not optional, and the ratio is graded on every write.
    //
    // The six class constants (regen, non-combat regen, crew, hull health, volatile health,
    // damage weight) are deliberately NOT touched. They are written from the class at creation and
    // there is no correct reason to move one afterwards; a setter for them would only ever be used
    // to author a record that looks vanilla and is not.
    //
    // Idempotent: a record already carrying every value is left untouched and reported. Validates
    // every target BEFORE mutating anything -- a lookup that fails halfway leaves a partly-patched
    // plugin, which is exactly what bit `conform` (it printed a success line over an untouched
    // disk).
    class gen_setshield
    {
        // ActorValue FormIDs, all Starfield.esm. READ OFF vanilla shield records via
        // `gen_inspect genericbaseform _Shields_`, not guessed and not carried from a sibling.
        const uint AV_SHIELD_HEALTH = 0x24A05F;
        const uint AV_SHIELD_MAX_HEALTH = 0x05BFA8;
        const uint AV_SPACESHIP_PART_MASS = 0x00ACDB;
        const uint AV_SHIELD_PART_MAX_POWER = 0x01ECCD;
        const uint AV_SYS_SHIELDS_HEALTH = 0x1EE8C9;
        const uint AV_SYS_SHIELDS_EM_HEALTH = 0x1EF0CC;

        // The ShipModuleClass<A|B|C> keywords, so the ceiling can be read off the record being
        // patched rather than asked for on the command line. Passing the class would let a caller
        // grade a class-A part against class C's ceiling by typing one wrong letter.
        static readonly Dictionary<uint, string> ClassKeywords = new()
        {
            { 0x0026FE57, "A" },
            { 0x0026FE56, "B" },
            { 0x0026FE55, "C" },
        };

        // Same table as ShieldSpec.Ceiling in gen_shipstruct, and the duplication is deliberate
        // rather than shared: this is a MEASUREMENT of vanilla, and the two commands must be able
        // to disagree loudly if someone edits one and not the other. See that file's comment for
        // the corpus, the numbers and the one excluded record (Vanguard_Bulwark_UC01).
        static readonly Dictionary<string, (float health, float ratio, float sysHealth)> Ceiling =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "A", (860f,  18.10f, 208f) },
                { "B", (1500f, 16.67f, 438f) },
                { "C", (1600f, 11.34f, 660f) },
            };

        public static int Generate(string[] args)
        {
            // args: [modname, "setshield", gbfm_editorids, health, mass, (power), (syshealth)]
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: setshield <modname> <gbfm_editorid>[,<gbfm_editorid>...] <health> <mass> [power] [syshealth]");
                Console.WriteLine("Health and mass are a pair -- the health/mass ratio is graded against the");
                Console.WriteLine("part's own ShipModuleClass keyword, so mass is required, not optional.");
                Console.WriteLine("Class A health 310..860, B 505..1500, C 680..1600 across vanilla.");
                return 1;
            }
            string modname = args[0];
            var targets = args[2].Split(',', StringSplitOptions.RemoveEmptyEntries)
                                 .Select(t => t.Trim()).ToList();

            if (!float.TryParse(args[3], out float health))
            {
                Console.WriteLine($"Error: '{args[3]}' is not a valid health");
                return 1;
            }
            if (!float.TryParse(args[4], out float mass))
            {
                Console.WriteLine($"Error: '{args[4]}' is not a valid mass");
                return 1;
            }
            float power = -1;
            if (args.Length >= 6 && !float.TryParse(args[5], out power))
            {
                Console.WriteLine($"Error: '{args[5]}' is not a valid power");
                return 1;
            }
            float sysHealth = -1;
            if (args.Length >= 7 && !float.TryParse(args[6], out sysHealth))
            {
                Console.WriteLine($"Error: '{args[6]}' is not a valid syshealth");
                return 1;
            }

            // A zero anywhere here is not a small shield -- zero health absorbs nothing while the
            // card still reads like a shield, and zero mass is the stealth buff in its purest
            // form. Refused rather than written, the same as --cargo's zero capacity.
            if (health <= 0 || mass <= 0)
            {
                Console.WriteLine($"Error: health and mass must both be positive (got {health}, {mass})");
                return 1;
            }
            if (args.Length >= 6 && power <= 0)
            {
                Console.WriteLine($"Error: power must be positive (got {power})");
                return 1;
            }
            if (args.Length >= 7 && sysHealth <= 0)
            {
                Console.WriteLine($"Error: syshealth must be positive (got {sysHealth})");
                return 1;
            }
            if (args.Length >= 6 && power > 12)
            {
                Console.WriteLine($"REFUSED: power {power} exceeds the 12-slot bar. No vanilla module of any"
                    + " class goes above it, and the bar itself caps there.");
                return 1;
            }

            if (modname == "Starfield")
            {
                Console.WriteLine("No way am I allowing you to edit Starfield.esm");
                return 1;
            }

            StarfieldMod myMod;
            string datapath;
            int changed = 0;

            // env holds the plugin open, so it is scoped to close before the write -- a same-path
            // WriteToBinary inside the using throws and leaves the old bytes looking like a
            // persisted no-op. Same reason as gen_setcargo / gen_setname / gen_setlightlayer.
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

                // Validate EVERY target before mutating anything, and that includes the balance
                // check -- refusing on the third of four targets after writing two is the
                // partly-patched plugin this pattern exists to prevent.
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
                    if (existing.Components?.OfType<PropertySheetComponent>().FirstOrDefault() == null)
                    {
                        Console.WriteLine($"Error: {existing.EditorID} has no PropertySheet -- refusing to invent one");
                        return 1;
                    }

                    // Grade against the part's OWN class keyword. A record with no class keyword
                    // is not a shield this command should be writing to -- it would author a
                    // 12-property shield sheet onto a structural part and the ship builder would
                    // list it under the wrong tab with stats nothing reads.
                    var kwc = existing.Components?.OfType<KeywordFormComponent>().FirstOrDefault();
                    string? cls = null;
                    if (kwc?.Keywords != null)
                    {
                        foreach (var kw in kwc.Keywords)
                        {
                            if (kw.FormKey.ModKey == sfKey
                                && ClassKeywords.TryGetValue(kw.FormKey.ID, out var c)) { cls = c; break; }
                        }
                    }
                    if (cls == null)
                    {
                        Console.WriteLine($"Error: {existing.EditorID} carries no ShipModuleClass<A|B|C> keyword,"
                            + " so its ceiling cannot be read off the record. Add one with `setkeyword`"
                            + " before retuning it -- guessing the class here would grade it against"
                            + " the wrong ladder.");
                        return 1;
                    }

                    var cap = Ceiling[cls];
                    bool over = false;
                    if (health > cap.health)
                    {
                        Console.WriteLine($"REFUSED ({existing.EditorID}): health {health} exceeds the class-{cls}"
                            + $" vanilla ceiling {cap.health} ({health / cap.health:0.00}x).");
                        over = true;
                    }
                    float ratio = health / mass;
                    if (ratio > cap.ratio)
                    {
                        Console.WriteLine($"REFUSED ({existing.EditorID}): health/mass {ratio:0.00} exceeds the"
                            + $" class-{cls} vanilla maximum {cap.ratio:0.00}. Mass is the brake -- under-massing"
                            + " a shield is a stealth buff to the whole ship, so this refuses even though the"
                            + " health alone is in range.");
                        over = true;
                    }
                    if (sysHealth > 0 && sysHealth > cap.sysHealth)
                    {
                        Console.WriteLine($"REFUSED ({existing.EditorID}): syshealth {sysHealth} exceeds the"
                            + $" class-{cls} vanilla ceiling {cap.sysHealth}.");
                        over = true;
                    }
                    if (over)
                    {
                        Console.WriteLine("  (These are the vanilla maxima, not a style guide. If you mean it,"
                            + " raise the ceiling in one place rather than passing a bigger number.)");
                        return 1;
                    }

                    Console.WriteLine($"  {existing.EditorID}: class {cls}, health {health} at mass {mass}"
                        + $" (health/mass {ratio:0.00}, class max {cap.ratio:0.00})");
                    found.Add(existing);
                }

                foreach (var existing in found)
                {
                    var gbfm = ((IGenericBaseFormGetter)existing).DeepCopy();
                    var sheet = gbfm.Components.OfType<PropertySheetComponent>().First();

                    // ShieldHealth == ShieldMaxHealth and SysHealth == SysEMHealth hold on every
                    // vanilla record, so both halves of each pair are written from one value. A
                    // max below the current health has no loud failure -- the shield simply never
                    // recharges to full -- which is why it is made unconstructible rather than
                    // left as a second parameter.
                    var writes = new List<(uint av, float value, string label)>
                    {
                        (AV_SHIELD_HEALTH, health, "ShieldHealth"),
                        (AV_SHIELD_MAX_HEALTH, health, "ShieldMaxHealth"),
                        (AV_SPACESHIP_PART_MASS, mass, "SpaceshipPartMass"),
                    };
                    if (power > 0) writes.Add((AV_SHIELD_PART_MAX_POWER, power, "SpaceshipShieldPartMaxPower"));
                    if (sysHealth > 0)
                    {
                        writes.Add((AV_SYS_SHIELDS_HEALTH, sysHealth, "ShipSystemShieldsHealth"));
                        writes.Add((AV_SYS_SHIELDS_EM_HEALTH, sysHealth, "ShipSystemShieldsEMHealth"));
                    }

                    bool touched = false;
                    foreach (var (av, value, label) in writes)
                    {
                        var key = new FormKey(sfKey, av);
                        var prop = sheet.Properties.FirstOrDefault(p => p.ActorValue.FormKey == key);
                        if (prop != null)
                        {
                            if (Math.Abs(prop.Value - value) < 0.0001f)
                            {
                                Console.WriteLine($"  {gbfm.EditorID}: {label} already {value} -- left as is");
                                continue;
                            }
                            Console.WriteLine($"  {gbfm.EditorID}: {label} {prop.Value} -> {value}");
                            prop.Value = value;
                        }
                        else
                        {
                            Console.WriteLine($"  {gbfm.EditorID}: + {label} = {value}");
                            sheet.Properties.Add(new ObjectProperty()
                            {
                                ActorValue = key.ToNullableLink<IActorValueInformationGetter>(),
                                Value = value,
                            });
                        }
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
