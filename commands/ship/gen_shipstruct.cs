using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    class gen_shipstruct
    {
        public static int Generate(string[] args)
        {
            Random random = new Random();
            StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string modelpath = args[4];

            // ---- optional flags; every one defaults to the previous hardcoded behaviour ----
            //
            // The defaults below (generic 1x1x1 all-faces snap, three vanilla paints, grid
            // bounds) are right for a 1x1x1 structural cube and wrong for anything else. A
            // SnapTemplate is a function of the part's SHAPE -- 2-3 nodes on a cockpit, 4 on a
            // wing or docker, 6 on the generic cube, 10 on a hab -- and a handed part (wing,
            // side engine) needs one PER SIDE.
            //
            // ENGINES: counted the population (19 ShipSnap_SMOD_Eng_* templates) rather than
            // trusting the old "engines run 1 node" note here, which was the MODE quoted as a
            // rule. Real spread is 1-7 nodes {1:6, 2:2, 3:4, 4:6, 7:1}, and only 12/19 carry a
            // Fore node. The node set follows HOW THE PART MOUNTS, not what kind of part it is:
            // a rear-mount engine gets a single Fore (Eng_Amun_Dunn-11); a handed side-pod gets
            // a single INBOARD Port/Starboard (Eng_Panoptes_DT10_Port carries a Starboard node
            // -- the face touching the hull, i.e. opposite its name); a flank-mountable barrel
            // gets the four side faces; a big structural engine gets the lot (Eng_Amun_Dunn-41:
            // Top x2, Bottom x2, Port, Starboard, Fore).
            //
            // OFFSETS: a SINGLE-node template sits at 0,0,0 -- the part is modelled so its
            // origin IS the attach point, so there is nothing to offset. A MULTI-node template
            // carries each face's true half-extent, because they cannot all sit at the origin.
            // Vanilla offsets only LOOK like grid values (+-4, +-1.75) because vanilla parts are
            // grid-sized; a 3.4844-diameter custom barrel takes +-1.7422 by the same rule.
            //
            //   --snap <EditorID>          link an existing SnapTemplate instead of the cube
            //   --snap-nodes <spec>        author one, named <prefix>_sntp_<item>. Spec is
            //                              face@x,y,z pairs separated by ';' --
            //                              e.g. "Starboard@-4,0,0" for a port wing whose
            //                              inboard face sits on the grid at x=-4.
            //                              Faces: Fore Aft Port Starboard Top Bottom.
            //   --category <EditorID|0xHEX>  the COBJ recipe filter -- which builder tab the
            //                              part shows under. Default Category_ShipMod_Structure
            //                              (0x0029C473). An EditorID resolves against the load
            //                              order; a mod's own category (e.g. Shipyards' custom
            //                              Category_ShipMod_AvontechStructure) is passed by id.
            //   --swaps <EditorID,...>     material swaps, replacing the three vanilla paints
            //   --bounds <minX,minY,minZ,maxX,maxY,maxZ>  ObjectBounds, min then max (a part is
            //                              not necessarily centred on its own origin)
            //   --mass <n>                 SpaceshipPartMass. Was hardcoded to 5.
            //   --variant <n>              ShipModuleVariant. Was hardcoded to 1. This is the
            //                              SHAPE axis, and it is NOT the same axis as the position
            //                              keyword: a handed pair (port + starboard of one shape)
            //                              SHARES a variant number, and a genuinely different
            //                              shape in the same flip set takes the next one.
            //                              Counted off vanilla SMS_Struct_ASC_Deimos_Wing_TypeA:
            //                              Port/Stbd = 10, Inwards pair = 15, Rev pair = 20,
            //                              Rev_Inwards pair = 25 -- so vanilla steps by 5, leaving
            //                              gaps to insert into. Avontech steps by 1; either is
            //                              fine, the engine does not care about the interval.
            //   --name "<display name>"    the GBFM FullName -- the string the ship builder shows
            //                              on the part card. Defaults to <item>, which is why
            //                              parts built before this flag shipped showing their
            //                              EditorID stub ("eng01") instead of a product name.
            //   --reuse-packin <EditorID>  build ONLY the GenericBaseForm + ConstructibleObject,
            //                              linking them to a PackIn that already exists. The
            //                              MoveableStatic, SnapTemplate, Cell and PackIn are all
            //                              shared, so a second GBFM/COBJ pair over the same PackIn
            //                              gives a VARIANT of an existing part -- the structural
            //                              (cosmetic) twin of an engine being the case it was
            //                              built for: same model, same flare, no engine stats, and
            //                              a Structure recipe filter instead of an Engine one.
            //                              Omit --engine on the variant and it falls back to the
            //                              plain 2-property sheet with no ShipModuleClass keyword,
            //                              which is what vanilla structural parts carry.
            //   --mstt-only                build ONLY the MoveableStatic + SnapTemplate. The Cell,
            //                              PackIn, GenericBaseForm and ConstructibleObject are all
            //                              skipped. THE EXACT COMPLEMENT OF --reuse-packin, and it
            //                              exists for the case --reuse-packin cannot serve: the CK
            //                              clones a VANILLA part's GBFM and PackIns far more
            //                              cheaply than this can author them, so for a class this
            //                              generator does not model (a HAB carries TWO PackIns --
            //                              SpaceshipLinkedExterior AND SpaceshipLinkedInterior --
            //                              and nothing here writes the second), the honest split
            //                              is to author the half that is ours and let the editor
            //                              clone the half that is Bethesda's.
            //                              CONFORM IS DELIBERATELY SKIPPED, and that is SAFE
            //                              rather than an omission: its two unauthorable fields
            //                              live on the GBFM (STRV) and on the PackIn's cell (the
            //                              XCLL tail word = 3), neither of which this path
            //                              authors -- and a CLONED VANILLA record already carries
            //                              both, because it shipped and it renders. The two
            //                              fields that ARE load-bearing here (Model.LightLayer = 1
            //                              and Flags.HasFirstPersonModel) sit on the
            //                              MoveableStatic and are authored below as normal.
            //   --no-snap                  author SnapTemplate = Null on the MoveableStatic.
            //                              THE DEFAULT IS NOT NEUTRAL: with no --snap and no
            //                              --snap-nodes this generator assigns the canonical
            //                              cube (0x00059B01), so every part it has ever built
            //                              carries six faces whether anyone chose them or not.
            //                              That is right for a STRUCTURAL part -- vanilla's own
            //                              SMOD_Struct_Deimos_Hull_A carries exactly that link
            //                              -- and WRONG for a HAB, which decomposes its snap
            //                              topology into six PLUG MoveableStatics placed in the
            //                              exterior PackIn's cell, each carrying its own
            //                              single-face SnapTemplate (SMOD_Plug_Deimos_Top at
            //                              0,0,+1.75 and its five siblings). The hab shell
            //                              itself is SnapTemplate Null; giving it the cube
            //                              would put a second set of faces on top of the plugs'.
            //                              Pairs with --mstt-only: clone the cell, get the six
            //                              plugs and their snaps, and leave the shell bare.
            //   --cargo <capacity>         make this part a CARGO hold: adds CarryWeight (the
            //                              capacity) and Health to the 2-property sheet. Counted
            //                              across all 91 vanilla SMS_Cargo GBFMs -- Health is 5 on
            //                              every one, and capacity/mass runs 4.67-6.10 (median
            //                              4.72). That ratio is a design guide, deliberately NOT
            //                              derived here: vanilla itself spans it, so picking the
            //                              number stays the author's call. Mutually exclusive with
            //                              --engine (different property sheets).
            //   --engine <k=v,...>         make this part an ENGINE: swaps the 2-property GBFM
            //                              sheet for the full 21-property engine sheet and adds
            //                              the ShipModuleClass<X> keyword. Keys:
            //                                class    A|B|C            (required)
            //                                force    thrust PER POWER (required)
            //                                thruster manoeuvre PER POWER (required)
            //                                power    engine power slots   (default 2)
            //                                tpower   thruster power slots (default 4)
            //                                health   engine + EM health   (default 64)
            //                                speed    max forward speed    (default by class)
            //                              e.g. --engine "class=A,force=5200,thruster=1000,power=2,health=70"
            //   --shield <k=v,...>         make this part a SHIELD generator: swaps the
            //                              2-property GBFM sheet for the full 12-property shield
            //                              sheet and adds the ShipModuleClass<X> keyword. Keys:
            //                                class     A|B|C           (required)
            //                                health    ShieldHealth    (required -- the dial)
            //                                mass      SpaceshipPartMass (required; it is part of
            //                                          the balance check below, so it is not left
            //                                          to --mass, which defaults silently to 5)
            //                                power     shield power slots       (default 3)
            //                                syshealth ShipSystemShieldsHealth   (default health/6)
            //                              e.g. --shield "class=A,health=310,mass=23,power=3,syshealth=50"
            //                              Mutually exclusive with --cargo and --engine.
            //
            // A SHIELD'S SHEET IS SIX CONSTANTS AND TWO DIALS, so the constants are written from
            // the CLASS rather than accepted as parameters. Measured 2026-09-03 over the 90
            // PURCHASABLE vanilla shield GBFMs in the load order (51 class A, 20 B, 19 C -- the
            // levelled ladders plus the rare Experimentals), and within a class there is NO
            // variation at all: ShieldRegenRate, ShieldRegenRateNonCombat, SpaceshipCrewRating
            // 0.5, generic Health 5, ShieldVolatileHealth 0 and ShipSystemDamageWeightShield 1.
            // A dial with one correct value per class is not a dial. The one excluded record and
            // why it has to be are documented on ShieldSpec.Ceiling below.
            //
            // Two further identities hold on all 91 records including the excluded one, and are
            // enforced here rather than left to the caller: ShieldHealth == ShieldMaxHealth, and
            // ShipSystemShieldsHealth == ShipSystemShieldsEMHealth.
            //
            // ⭐ VANILLA PUTS THE CLASS IN THE EDITORID, NOT ONLY IN A KEYWORD --
            // SMA_/SMB_/SMC_Shields_<mfr>_<name>_lvlNN. That is why a search for "SMS_Shield"
            // returns four records from a Creation and none of the base game's; the SMS_ prefix
            // that every other module class uses does not apply to shields. The class prefix is a
            // naming convention, and we still carry ShipModuleClass<X>, which is what the builder
            // actually gates on.
            //
            // ⛔ AND THE LEVEL GATE IS NOT IN THE GBFM AT ALL. The _lvlNN in a vanilla EditorID is
            // a LABEL; the real gate is a GetLevel condition on the COBJ, and the two do not even
            // agree -- co_SMA_Shields_Sextant_Deflector_SG-30_Set_lvl18 gates at >= 14. A part
            // built here is UNGATED. That is a product decision (his, 2026-09-03: "people want to
            // see the parts on new characters"), and its consequence is enforced below: an
            // ungated part must land at the BOTTOM of its class, or it is the SAL-6830 break --
            // the strictly-best thing available at level 1, which does not win, it deletes the
            // tier under it. Use `setcondition` if you ever want a gate.
            //
            // ENGINE STATS ARE STORED PER POWER. SpaceshipEnginePartForce and
            // SpaceshipThrusterPartForce hold the per-power value; the ship builder shows
            // force x power. Mass and health are absolute per module. Verified three ways
            // against vanilla: Ares DT30 force 4600 x power 2 = the 9200 the game displays;
            // Amun Dunn X-300 (8630 x 3 = 25890); Dunn-71 (8860 x 3 = 26580).
            //
            // The engine power bar caps at 12, so TOTAL fleet thrust is exactly
            // (force per power) x 12 regardless of how many modules you fit -- module count is
            // decorative. That makes per-power the only currency, and it is why the ceilings
            // below are expressed in it.
            //
            // Each authored node is a VERBATIM copy of the matching node on vanilla's
            // ShipSnap_SMOD_Generic_1x1x1_All01 with only its Offset moved, so the node
            // record and its rotation come from the game, never from here. Rotation is a
            // property of the face (confirmed: the Nova wing templates carry the identical
            // rotations for their Starboard/Port/Aft nodes).
            string? optSnap = null, optSnapNodes = null, optSwaps = null, optBounds = null, optCategory = null;
            string? optEngine = null, optMass = null, optName = null, optReusePackin = null, optDesc = null;
            string? optVariant = null, optCargo = null, optShield = null;
            bool optMsttOnly = false, optNoSnap = false;
            for (int i = 5; i < args.Length; i++)
            {
                bool hasValue = i + 1 < args.Length;
                switch (args[i])
                {
                    case "--snap": if (!hasValue) { Console.WriteLine("Error: --snap needs a value"); return 1; } optSnap = args[++i]; break;
                    case "--snap-nodes": if (!hasValue) { Console.WriteLine("Error: --snap-nodes needs a value"); return 1; } optSnapNodes = args[++i]; break;
                    case "--swaps": if (!hasValue) { Console.WriteLine("Error: --swaps needs a value"); return 1; } optSwaps = args[++i]; break;
                    case "--bounds": if (!hasValue) { Console.WriteLine("Error: --bounds needs a value"); return 1; } optBounds = args[++i]; break;
                    case "--category": if (!hasValue) { Console.WriteLine("Error: --category needs a value"); return 1; } optCategory = args[++i]; break;
                    case "--engine": if (!hasValue) { Console.WriteLine("Error: --engine needs a value"); return 1; } optEngine = args[++i]; break;
                    case "--mass": if (!hasValue) { Console.WriteLine("Error: --mass needs a value"); return 1; } optMass = args[++i]; break;
                    case "--cargo": if (!hasValue) { Console.WriteLine("Error: --cargo needs a value"); return 1; } optCargo = args[++i]; break;
                    case "--shield": if (!hasValue) { Console.WriteLine("Error: --shield needs a value"); return 1; } optShield = args[++i]; break;
                    case "--variant": if (!hasValue) { Console.WriteLine("Error: --variant needs a value"); return 1; } optVariant = args[++i]; break;
                    case "--name": if (!hasValue) { Console.WriteLine("Error: --name needs a value"); return 1; } optName = args[++i]; break;
                    case "--desc": if (!hasValue) { Console.WriteLine("Error: --desc needs a value"); return 1; } optDesc = args[++i]; break;
                    case "--reuse-packin": if (!hasValue) { Console.WriteLine("Error: --reuse-packin needs a value"); return 1; } optReusePackin = args[++i]; break;
                    case "--mstt-only": optMsttOnly = true; break;
                    case "--no-snap": optNoSnap = true; break;
                    default: Console.WriteLine("Error: unknown option " + args[i]); return 1;
                }
            }

            // --reuse-packin authors neither the MoveableStatic nor the SnapTemplate, so any flag
            // that configures those is silently ignored on that path. Refuse rather than accept it
            // and quietly do nothing -- a flag that appears to work is worse than one that errors.
            if (optReusePackin != null)
            {
                var ignored = new List<string>();
                if (optSnap != null) ignored.Add("--snap");
                if (optSnapNodes != null) ignored.Add("--snap-nodes");
                if (optSwaps != null) ignored.Add("--swaps");
                if (optBounds != null) ignored.Add("--bounds");
                if (ignored.Count > 0)
                {
                    Console.WriteLine("Error: " + string.Join(", ", ignored)
                        + " configure the MoveableStatic/SnapTemplate, which --reuse-packin does not author."
                        + " They belong on the run that built the original part.");
                    return 1;
                }
            }

            // --mstt-only is the exact complement of --reuse-packin, so its guard is the exact
            // mirror: it authors neither the GenericBaseForm nor the ConstructibleObject, so
            // every flag that configures those must REFUSE rather than be silently ignored.
            // Same reasoning as above -- a flag that appears to work is worse than one that errors.
            // --no-snap and --snap/--snap-nodes are a direct contradiction: one says author no
            // template, the others say author this one. Refuse rather than let precedence
            // decide it silently -- whichever way it fell, half the callers would be wrong and
            // the record would look deliberate either way.
            if (optNoSnap && (optSnap != null || optSnapNodes != null))
            {
                Console.WriteLine("Error: --no-snap contradicts "
                    + (optSnap != null ? "--snap" : "--snap-nodes")
                    + ". One says author no SnapTemplate, the other says author that one. Pick one.");
                return 1;
            }
            if (optNoSnap && optReusePackin != null)
            {
                Console.WriteLine("Error: --no-snap configures the MoveableStatic,"
                    + " which --reuse-packin does not author.");
                return 1;
            }

            if (optMsttOnly && optReusePackin != null)
            {
                Console.WriteLine("Error: --mstt-only and --reuse-packin are opposites."
                    + " --mstt-only builds the MoveableStatic + SnapTemplate and nothing else;"
                    + " --reuse-packin builds the GBFM + COBJ and nothing else. Pick one.");
                return 1;
            }
            if (optMsttOnly)
            {
                var ignored = new List<string>();
                if (optCargo != null) ignored.Add("--cargo");
                if (optEngine != null) ignored.Add("--engine");
                if (optShield != null) ignored.Add("--shield");
                if (optMass != null) ignored.Add("--mass");
                if (optVariant != null) ignored.Add("--variant");
                if (optName != null) ignored.Add("--name");
                if (optDesc != null) ignored.Add("--desc");
                if (optCategory != null) ignored.Add("--category");
                if (ignored.Count > 0)
                {
                    Console.WriteLine("Error: " + string.Join(", ", ignored)
                        + " configure the GenericBaseForm/ConstructibleObject, which --mstt-only does not author."
                        + " They belong on the record you clone in the Creation Kit.");
                    return 1;
                }
            }

            // --cargo <capacity>: make this a CARGO part (CarryWeight + Health on the sheet).
            // Refuses a non-positive capacity -- a zero-capacity hold is exactly the silent
            // nothing this flag exists to prevent, so it must never be writable by accident.
            float cargoCapacity = 0;
            if (optCargo != null)
            {
                if (!float.TryParse(optCargo, out cargoCapacity))
                {
                    Console.WriteLine("Error: --cargo wants a number (the hold's CarryWeight capacity)");
                    return 1;
                }
                if (cargoCapacity <= 0)
                {
                    Console.WriteLine($"Error: --cargo capacity must be positive (got {cargoCapacity})");
                    return 1;
                }
            }
            // The three module-class flags are three different PropertySheets, not three sets of
            // fields that compose. Named individually so one run reports every clash rather than
            // the first one found.
            {
                var sheets = new List<string>();
                if (optCargo != null) sheets.Add("--cargo");
                if (optEngine != null) sheets.Add("--engine");
                if (optShield != null) sheets.Add("--shield");
                if (sheets.Count > 1)
                {
                    Console.WriteLine("Error: " + string.Join(" and ", sheets)
                        + " are different property sheets; pick one");
                    return 1;
                }
            }

            EngineSpec? engine = null;
            if (optEngine != null)
            {
                engine = EngineSpec.Parse(optEngine);
                if (engine == null) return 1;                 // Parse prints the reason
            }

            ShieldSpec? shield = null;
            if (optShield != null)
            {
                shield = ShieldSpec.Parse(optShield);
                if (shield == null) return 1;                 // Parse prints the reason
            }

            // --shield carries its own mass because the mass is HALF the balance check (a shield
            // that is light for its health is a stealth buff to the whole ship, the same way an
            // under-massed engine is). Accepting it from both places would let the refusal grade
            // one number while the record shipped another.
            if (optShield != null && optMass != null)
            {
                Console.WriteLine("Error: --shield carries mass= and is graded on it;"
                    + " --mass would set a second, ungraded value. Put the mass in --shield.");
                return 1;
            }

            float partMass = 5;
            if (optMass != null && !float.TryParse(optMass, out partMass))
            {
                Console.WriteLine("Error: --mass wants a number");
                return 1;
            }
            if (shield != null) partMass = shield.Mass;
            float partVariant = 1;
            if (optVariant != null && !float.TryParse(optVariant, out partVariant))
            {
                Console.WriteLine("Error: --variant wants a number");
                return 1;
            }
            if (optSnap != null && optSnapNodes != null)
            {
                Console.WriteLine("Error: --snap links an existing template and --snap-nodes authors a new one; pick one");
                return 1;
            }

            string datapath = "";
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();
                datapath = env.DataFolderPath;
                //Find the modkey 
                ModKey newMod = new ModKey(modname, ModType.Master);
                myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                if (!env.LoadOrder.ModExists(newMod))
                {
                    myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                }
                else
                {
                    for (int i = 0; i < env.LoadOrder.Count; i++)
                    {
                        if (env.LoadOrder[i].FileName == modname + ".esm")
                        {
                            ModPath modPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                            gen_quest_main.FixNextFormId(myMod);

                            //Check if this mod already contains this entry
                            foreach ( var ms in myMod.MoveableStatics)
                            {
                                if (ms.EditorID == prefix + "_ms_" + item)
                                {
                                    Console.WriteLine("Error, mod already contains : " + prefix + "_ms_" + item);
                                    return 1;
                                }
                            }
                    
                        }
                    }
                }

                // ---- variant path: reuse an existing PackIn --------------------------------
                // A second GBFM/COBJ pair over the SAME PackIn is a VARIANT of a part that already
                // exists -- the structural (cosmetic) twin of an engine being the case this was
                // built for. The MoveableStatic, SnapTemplate, Cell and everything placed in it
                // (including the engine flare, which reads ship state on its own and behaves
                // correctly on a structural part) are all shared, so the entire first half of this
                // generator is skipped and only the two records that actually differ get authored.
                // The GBFM/COBJ tail below depends on exactly one thing from that half: `packin`.
                //
                // The branch starts HERE, above the MoveableStatic log line, so the run cannot
                // announce records it did not build -- output is documentation, and a log that
                // claims a record was created is as false as a doc that describes a field that
                // does not exist. (It printed exactly that lie once before this was moved.)
                // Nullable since --mstt-only authors no PackIn at all. It is read only inside
                // the GBFM/COBJ tail, which that same flag skips, so the null cannot escape.
                PackIn? packin = null;
                if (optReusePackin != null)
                {
                    var reused = myMod.PackIns.FirstOrDefault(
                        p => string.Equals(p.EditorID, optReusePackin, StringComparison.OrdinalIgnoreCase));
                    if (reused == null)
                    {
                        // Fail loud -- a typo'd PackIn must not silently produce an orphan part.
                        Console.WriteLine("Error: no PackIn with EditorID '" + optReusePackin + "' in " + modname);
                        return 1;
                    }
                    packin = reused;
                    Console.WriteLine("Reusing PackIn : " + optReusePackin + "  (variant -- GBFM + COBJ only)");
                }
                else
                {

                // Moveable Static ------------------------------------------
                Console.WriteLine("Building Record : " + prefix + "_ms_" + item);
                IFormLinkNullable<ISnapTemplateGetter> snaplink = new FormKey(env.LoadOrder[0].ModKey, 0x00059B01).ToNullableLink<ISnapTemplateGetter>();
                IFormLinkNullable<IKeywordGetter> spaceshipformshipmodule = new FormKey(env.LoadOrder[0].ModKey, 0x001BB401).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> NavmeshUseDefaultCollisionForGeneration = new FormKey(env.LoadOrder[0].ModKey, 0x00207960).ToNullableLink<IKeywordGetter>();

                byte[] flldarry = new byte[4] { 1, 0, 0, 0 };
                byte[] xflgarry = new byte[1] { 2 };

                // ---- snap template: link a named one, or author one from a node spec ----
                if (optSnap != null)
                {
                    var found = FindSnapTemplate(myMod, env, optSnap);
                    if (found == null)
                    {
                        Console.WriteLine("Error: no SnapTemplate with EditorID '" + optSnap + "' in " + modname + " or Starfield.esm");
                        return 1;
                    }
                    snaplink = found.ToNullableLink<ISnapTemplateGetter>();
                    Console.WriteLine("Snap template : linking " + optSnap);
                }
                else if (optSnapNodes != null)
                {
                    var authored = BuildSnapTemplate(myMod, env, prefix + "_sntp_" + item, optSnapNodes);
                    if (authored == null) return 1;
                    myMod.SnapTemplates.Add(authored);
                    snaplink = authored.ToNullableLink<ISnapTemplateGetter>();
                    Console.WriteLine("Building Record : " + authored.EditorID + " (" + authored.Nodes.Count + " node(s))");
                }

                // ---- material swaps ----
                var swaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>();
                if (optSwaps != null)
                {
                    foreach (var editorId in optSwaps.Split(',', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var swap = FindMaterialSwap(myMod, env, editorId.Trim());
                        if (swap == null)
                        {
                            Console.WriteLine("Error: no LayeredMaterialSwap with EditorID '" + editorId.Trim() + "' in " + modname + " or Starfield.esm");
                            return 1;
                        }
                        swaps.Add(swap);
                    }
                    Console.WriteLine("Material swaps : " + optSwaps);
                }
                else
                {
                    // NO DEFAULT SWAPS -- deliberately. This used to attach three VANILLA
                    // Starfield paints (0x099196 / 0x0B6B1F / 0x2AF78A), which was a
                    // Shipyards-ism: those swap FROM vanilla materials, so they work only on a
                    // part that USES vanilla materials. On a part with its own .mat the source
                    // key never matches and the part is render-blocked -- it cost a full day on
                    // atsd_wing01 (2026-07-22) and every part built here inherited the trap
                    // silently.
                    //
                    // A part with no swaps renders correctly; it simply offers no paint option
                    // until real swaps are wired. That is the honest default: a missing feature
                    // you are told about beats a broken render you are not.
                    Console.WriteLine("Material swaps : none (no --swaps given)");
                    Console.WriteLine("  NOTE: this part will render but will NOT be paintable until swaps are wired.");
                    Console.WriteLine("        A LayeredMaterialSwap is REFL-opaque and cannot be authored here --");
                    Console.WriteLine("        deep-copy one with 'copyswap <mod> <mstt> <new>=<src>' and repoint it in the CK.");
                }

                // ---- object bounds ----
                // Six numbers, min then max -- NOT half-extents, because a part is not
                // necessarily centred on its own origin (a wing sits entirely outboard).
                var boundsFirst = new P3Float(-4, -4, -1.767578f);
                var boundsSecond = new P3Float(4, 4, 1.767578f);
                if (optBounds != null)
                {
                    var parts = optBounds.Split(',');
                    var n = new float[6];
                    if (parts.Length != 6)
                    {
                        Console.WriteLine("Error: --bounds wants six numbers, minX,minY,minZ,maxX,maxY,maxZ");
                        return 1;
                    }
                    for (int i = 0; i < 6; i++)
                    {
                        if (!float.TryParse(parts[i], out n[i]))
                        {
                            Console.WriteLine("Error: --bounds value '" + parts[i] + "' is not a number");
                            return 1;
                        }
                    }
                    boundsFirst = new P3Float(n[0], n[1], n[2]);
                    boundsSecond = new P3Float(n[3], n[4], n[5]);
                }

                MoveableStatic moveableStatic = new MoveableStatic(myMod);
                moveableStatic.EditorID = prefix + "_ms_" + item;
                moveableStatic.ObjectBounds = new ObjectBounds()
                {
                    First = boundsFirst,
                    Second = boundsSecond
                };
                // SetToNull rather than skipping the assignment: the property is not optional
                // on the record, so what is being authored here is an explicit Null -- the same
                // shape vanilla's own hab shells carry -- never an absent field.
                if (optNoSnap) snaplink.SetToNull();
                moveableStatic.SnapTemplate = snaplink;
                moveableStatic.Model = new Model()
                {
                    File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(modelpath),
                    MaterialSwaps = swaps,
                    // "Support Model Only Swap" (xEdit) / HasFirstPersonModel (Mutagen's mislabel) -- REQUIRED for
                    // a ship part to be recolourable in the builder. Every vanilla structural part AND the Sherpa
                    // set it; a part without it renders + attaches + shows its base colour but offers NO paint
                    // option (2026-07-22, the wing recolour hunt -- found by stepping a vanilla field-by-field).
                    Flags = Model.Flag.HasFirstPersonModel,
                    // LightLayer (subrecord FLLD) -- REQUIRED for a ship part to RENDER AT ALL. A Model
                    // without it builds, attaches, flips and paints, and draws NOTHING in the ship
                    // builder (2026-07-30, atsd_vent01_rear -- the first part ever taken to the glass
                    // without a CK save in between). Vanilla SMOD_Struct_Deimos_Hull_A carries
                    // LightLayer 1, and so do all 13 Stardust parts that render -- every one of which
                    // had been through a CK save, which writes the field. The omission was MASKED by
                    // the workflow, not absent: "we have shipped 13 parts" was never a test, because
                    // the variable was never isolated.
                    //
                    // Second instance of this exact defect class -- the Flags line above is the same
                    // shape (a Model sub-field the generator never set, invisible until someone diffed
                    // a vanilla part field-by-field). If a third turns up, this block wants a
                    // vanilla-conformance check rather than another hand-added line.
                    LightLayer = 1,
                };
                moveableStatic.DATA = 4;
                moveableStatic.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                {
                    spaceshipformshipmodule,
                    NavmeshUseDefaultCollisionForGeneration
                };
                myMod.MoveableStatics.Add(moveableStatic);

                // ---- --mstt-only stops HERE ---------------------------------------------
                // Above this line is ours (the model, its swaps, its snap table); below it is
                // the record scaffolding the Creation Kit clones more cheaply from vanilla.
                // The log line sits INSIDE the branch for the same reason the --reuse-packin
                // one does: output is documentation, and a run must not announce a record it
                // did not build.
                if (optMsttOnly)
                {
                    Console.WriteLine("--mstt-only : Cell, PackIn, GenericBaseForm and ConstructibleObject NOT authored");
                    Console.WriteLine("              clone them in the CK and point the exterior PackIn at "
                                      + prefix + "_ms_" + item);
                }
                else
                {

                //Cell---------------------------

                /*
                ElminsterAU
                Block and sub-block address the last 2 digits of the object id of the record converted to decimal
                You have no control over these 
                Then it's broken. Because the game engine depends on the records being in the correct block/sub-block to find them
                */
                IFormLinkNullable<IImageSpaceGetter> DefaultImagespacePackin = new FormKey(env.LoadOrder[0].ModKey, 0x0006AD68).ToNullableLink<IImageSpaceGetter>();
                Console.WriteLine("Building Record : " + prefix + "_cell_" + item);
                var newCell = new Cell(myMod)
                {
                    EditorID = prefix + "_cell_" + item,
                    Temporary = new ExtendedList<IPlaced>(),
                    Flags = Cell.Flag.IsInteriorCell,
                    // A PackIn's storage cell is what the ship builder renders the module OUT OF,
                    // and a cell with no lighting draws nothing (2026-07-30, atsd_vent01_rear).
                    // FarHeightRange and Unknown2 were MISSING here: every CK-saved cell carries
                    // 10000 and 3 in those two words, every generated one carried 0 and 0. Read off
                    // the working sibling word-by-word, not guessed.
                    Lighting = new CellLighting()
                    {
                        DirectionalFade = 1,
                        FogPower = 1,
                        FogMax = 1,
                        NearHeightRange = 10000,
                        FarHeightRange = 10000,
                        Unknown1 = 1951,
                    },
                    // LTMP. Null-valued, but the SUBRECORD must exist -- absent on every generated
                    // cell, present on all thirteen that render.
                    LightingTemplate = FormKey.Null.ToNullableLink<ILightingTemplateGetter>(),
                    WaterHeight = 0,
                    XILS = 1.0f,
                    XCLAs = new ExtendedList<CellXCLAItem>()
                    {
                        new CellXCLAItem()
                        {
                            XCLA = 1,
                            XCLD = "Default Layer Name 1"
                        },
                        new CellXCLAItem()
                        {
                            XCLA = 2,
                            XCLD = "Default Layer Name 2"
                        },
                        new CellXCLAItem()
                        {
                            XCLA = 3,
                            XCLD = "Default Layer Name 3"
                        },
                        new CellXCLAItem()
                        {
                            XCLA = 4,
                            XCLD = "Default Layer Name 4"
                        },
                    },
                    ImageSpace = DefaultImagespacePackin,

                };
                var key = newCell.FormKey.ID;
                var stringkey = key.ToString();
                var cellblockNumber = int.Parse(stringkey.Substring(stringkey.Length - 1));
                var subBlockNumber = int.Parse(stringkey.Substring(stringkey.Length - 2, 1));

                //Try and use existing cellblocks and subblocks first.
                CellBlock? cellblock = null;
                bool newCellBlock = false;
                for( int i = 0; i < myMod.Cells.Count; i++ )
                {
                    if (myMod.Cells[i].BlockNumber == cellblockNumber )
                    {
                        cellblock = myMod.Cells[i];
                    }
                }
                if (cellblock == null )
                {
                    cellblock = new CellBlock
                    {
                        BlockNumber = cellblockNumber,
                        GroupType = GroupTypeEnum.InteriorCellBlock,
                        SubBlocks = new ExtendedList<CellSubBlock>()
                    };
                    newCellBlock = true;
                }

                bool addSubblock = true;
                for(int i = 0; i < cellblock.SubBlocks.Count; i++ )
                {
                    if (cellblock.SubBlocks[i].BlockNumber == subBlockNumber)
                    {
                        addSubblock = false;
                    }
                }
                if (addSubblock)
                {
                    cellblock.SubBlocks.Add(new CellSubBlock()
                    {
                        BlockNumber = subBlockNumber,
                        GroupType = GroupTypeEnum.InteriorCellSubBlock,
                        Cells = new ExtendedList<Cell>()
                    });
                }


                // Cell contents -------------------------------
                IFormLink<IPlaceableObjectGetter> OutpostGroupPackinDummy = new FormKey(env.LoadOrder[0].ModKey, 0x00015804).ToLink<IPlaceableObjectGetter>();
                IFormLink<IPlaceableObjectGetter> PrefabPackinPivotDummy = new FormKey(env.LoadOrder[0].ModKey, 0x0003F808).ToLink<IPlaceableObjectGetter>();
                IFormLink<IKeywordGetter> UpdatesDynamicNavmeshKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x00140158).ToLink<IKeywordGetter>();
                Console.WriteLine("Building Cell Contents");
                newCell.Temporary.Add(new PlacedObject(myMod)
                {
                    Base = OutpostGroupPackinDummy,
                    Position = new P3Float(0, 0, 0),
                    Rotation = new P3Float(0, 0, 0)
                });
                newCell.Temporary.Add(new PlacedObject(myMod)
                {
                    Base = PrefabPackinPivotDummy,
                    Position = new P3Float(0, 0, 0),
                    Rotation = new P3Float(0, 0, 0)
                });
                var cell_contents_components = new ExtendedList<AComponent>()
                {
                    new KeywordFormComponent()
                    {
                        Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                        {
                            UpdatesDynamicNavmeshKeyword
                        }
                    }
                };
                newCell.Temporary.Add(new PlacedObject(myMod)
                {
                    Base = moveableStatic.ToLink<IPlaceableObjectGetter>(),
                    //Not sure we need ragdoll data, but just copying what I know works
                    RagdollData = new ExtendedList<RagdollData>()
                    {
                        new RagdollData()
                        {
                            BoneId = 0,
                            Position = new P3Float(0, 0, 0),
                            Rotation = new P3Float(0, 0, 0)
                        }
                    },
                    Components = cell_contents_components,
                    Position = new P3Float(0, 0, 0),
                    Rotation = new P3Float(0, 0, 0)
                });
                
                bool addedCell = false;
                for (int i = 0; i < cellblock.SubBlocks.Count && !addedCell; i++)
                {
                    if (cellblock.SubBlocks[i].BlockNumber == subBlockNumber)
                    {
                        cellblock.SubBlocks[i].Cells.Add(newCell);
                        addedCell = true;
                    }
                }
                if(newCellBlock)
                {
                    myMod.Cells.Add(cellblock);
                }


                // Packin --------------------------------------
                Console.WriteLine("Building Record : " + prefix + "_pkn_" + item);
                IFormLink<ITransformGetter> link = new FormKey(env.LoadOrder[0].ModKey, 0x00050FAC).ToLink<ITransformGetter>();

                byte[] barray = new byte[4] { 14, 00, 00, 00 };
                packin = new PackIn(myMod)
                {
                    EditorID = prefix + "_pkn_" + item,
                    // Was HARDCODED to the 1x1x1 grid box while --bounds reached only the
                    // MoveableStatic -- so a part whose geometry sits outboard of the grid (a face
                    // plate, a wing) declared a PackIn box its own model was almost entirely
                    // outside. The CK recomputes this on save, which is why it never surfaced.
                    ObjectBounds = new ObjectBounds()
                    {
                        First = boundsFirst,
                        Second = boundsSecond
                    },
                    Transforms = new Transforms
                    {
                        Ship = link
                    },
                    Filter = "\\Ships\\Modules\\Exterior\\Struct\\Deimos\\",
                    Cell = newCell.ToNullableLink<ICellGetter>(),
                    Version = 0,
                    FNAM = new MemorySlice<byte>(barray),
                    MaterialSwaps = new ExtendedList<IFormLinkGetter<ILayeredMaterialSwapGetter>>()
                };
                myMod.PackIns.Add(packin);

                }   // end of the Cell+PackIn half (skipped entirely by --mstt-only)

                }   // end of the full-build path (skipped entirely by --reuse-packin)

                // The condition is `packin != null` rather than `!optMsttOnly` DELIBERATELY: this
                // tail's whole dependency on the half above it is the PackIn it links the GBFM to
                // (SpaceshipLinkedExterior, below), so testing for the thing it needs states the
                // real precondition where testing the flag only states the reason it might be
                // missing. Exactly equivalent on all three paths -- --reuse-packin assigns it and
                // refuses on a miss, the full build assigns it, --mstt-only leaves it null -- and
                // it is what lets the compiler prove the deref below, so the null cannot escape
                // without a null-forgiving `!` that a later edit would silently invalidate.
                if (packin != null)
                {

                //Generic Base Form -------------------------------------------
                IFormLinkNullable<IGenericBaseFormTemplateGetter> FormSpaceshipModule = new FormKey(env.LoadOrder[0].ModKey, 0x0003058E).ToNullableLink<IGenericBaseFormTemplateGetter>();
                IFormLinkNullable<IActorValueInformationGetter> SpaceshipPartMass = new FormKey(env.LoadOrder[0].ModKey, 0x0000ACDB).ToNullableLink<IActorValueInformationGetter>();
                IFormLinkNullable<IActorValueInformationGetter> ShipModuleVariant = new FormKey(env.LoadOrder[0].ModKey, 0x0027BACE).ToNullableLink<IActorValueInformationGetter>();
                IFormLinkNullable<IKeywordGetter> SpaceshipLinkedExterior = new FormKey(env.LoadOrder[0].ModKey, 0x0000662F).ToNullableLink<IKeywordGetter>();
                IFormLinkNullable<IKeywordGetter> ShipModuleManufacturerDeimos = new FormKey(env.LoadOrder[0].ModKey, 0x001462C0).ToNullableLink<IKeywordGetter>();
                Console.WriteLine("Building Record : " + prefix + "_gbfm_" + item);

                // The PropertySheet is SPARSE -- a cargo GBFM carries 3 properties, an engine 21.
                // Anything absent is simply not authored, so build exactly the set this part needs.
                ObjectProperty Prop(uint av, float value) => new ObjectProperty()
                {
                    ActorValue = new FormKey(env.LoadOrder[0].ModKey, av).ToNullableLink<IActorValueInformationGetter>(),
                    Value = value,
                };

                var properties = new ExtendedList<ObjectProperty>()
                {
                    new ObjectProperty() { ActorValue = SpaceshipPartMass, Value = partMass },
                    new ObjectProperty() { ActorValue = ShipModuleVariant, Value = partVariant },
                };

                if (optCargo != null)
                {
                    // A cargo GBFM carries THREE properties: CarryWeight, SpaceshipPartMass and
                    // Health. Counted across all 91 vanilla SMS_Cargo records -- Health is 5 on
                    // every one, and CarryWeight / SpaceshipPartMass runs 4.67 (min) 4.72 (median)
                    // 6.10 (max), i.e. capacity is ~4.7x mass across the whole catalogue.
                    //
                    // The ratio is NOT applied here. It is the design guide for picking the
                    // number; deriving capacity from mass would turn a measurement into a law and
                    // take the choice away from the author, and vanilla itself spans 4.67-6.10.
                    //
                    // CarryWeight is the whole point of the part. Without it the record builds,
                    // renders, snaps and paints, and the hold carries nothing -- a silent nothing,
                    // which is the same shape as the Model.LightLayer gap and is why this is a
                    // generator flag rather than a step someone has to remember.
                    properties.Add(Prop(0x000002DC, cargoCapacity));   // CarryWeight
                    properties.Add(Prop(0x000002D4, 5));               // Health (generic)
                    Console.WriteLine("           cargo capacity " + cargoCapacity + " (CarryWeight), health 5");
                }

                if (engine != null)
                {
                    // The 19 engine-specific properties. Constants are the vanilla class-A
                    // reference (Ares DT30, SMA_Engine_Panoptes_Ares_DT30_Stb_lvl16) read off
                    // Starfield.esm rather than invented -- backward speed 32, strafe speed 50,
                    // strafe force 19000, boost 3/2/0.3, crew 0.25, generic Health 5, the three
                    // Max*Velocity all 0 (every engine sampled zeroes them).
                    properties.Add(Prop(0x0000ACDC, engine.Force));          // SpaceshipEnginePartForce      (PER POWER)
                    properties.Add(Prop(0x0000ACDD, engine.Power));          // SpaceshipEnginePartMaxPower
                    properties.Add(Prop(0x0000ACDE, engine.Thruster));       // SpaceshipThrusterPartForce    (PER POWER)
                    properties.Add(Prop(0x0000ACDF, engine.ThrusterPower));  // SpaceshipThrusterPartMaxPower
                    properties.Add(Prop(0x00278988, engine.Speed));          // ...MaxForwardSpeed
                    properties.Add(Prop(0x00278986, 32));                    // ...MaxBackwardSpeed
                    properties.Add(Prop(0x002A9542, 19000));                 // ThrusterPartStrafeForce
                    properties.Add(Prop(0x00278987, 50));                    // ThrusterPartMaxStrafeSpeed
                    properties.Add(Prop(0x001EF0CD, engine.Health));         // ShipSystemEngineHealth
                    properties.Add(Prop(0x001EF0C2, engine.Health));         // ShipSystemEngineEMHealth (always equal)
                    properties.Add(Prop(0x00011589, 1));                     // ShipSystemDamageWeightEngine
                    properties.Add(Prop(0x00001885, 3));                     // SpaceshipBoostFuel
                    properties.Add(Prop(0x00001886, 2));                     // SpaceshipBoostSpeed
                    properties.Add(Prop(0x0006A256, 0.3f));                  // SpaceshipBoostRechargeRate
                    properties.Add(Prop(0x00019080, 0.25f));                 // SpaceshipCrewRating
                    properties.Add(Prop(0x000002D4, 5));                     // Health (generic)
                    properties.Add(Prop(0x002DF170, 0));                     // ...MaxPitchVelocity
                    properties.Add(Prop(0x002DF171, 0));                     // ...MaxRollVelocity
                    properties.Add(Prop(0x002E6679, 0));                     // ...MaxYawVelocity
                }

                if (shield != null)
                {
                    // The 11 shield-specific properties (mass is already on the sheet above).
                    // Every ActorValue FormID here was READ off a vanilla shield GBFM via
                    // gen_inspect, not carried from a sibling constant and not guessed.
                    //
                    // Six of them are CLASS CONSTANTS, measured over the purchasable vanilla corpus
                    // (2026-09-03) with zero within-class variation -- see the header. They are
                    // written from the class rather than exposed, because a dial with one correct
                    // value is not a dial, and an exposed one is a way to author a record that
                    // looks vanilla and is not (the ats_gbfm_warp02_01 failure: every individual
                    // number plausible, four axes over at once, invisible for a year).
                    //
                    // ⭐ TWO IDENTITIES ARE ENFORCED RATHER THAN PASSED. ShieldHealth ==
                    // ShieldMaxHealth on all 91 records, and ShipSystemShieldsHealth ==
                    // ...EMHealth on all 91. Taking either as two parameters would make the
                    // wrong record CONSTRUCTIBLE, and a shield whose max is below its current
                    // health has no loud failure -- it just never recharges to full.
                    properties.Add(Prop(0x0005BFA7, shield.RegenRate));       // ShieldRegenRate
                    properties.Add(Prop(0x000090A9, shield.RegenRateNonCombat)); // ...NonCombat
                    properties.Add(Prop(0x0024A05F, shield.Health));          // ShieldHealth      *the dial*
                    properties.Add(Prop(0x0005BFA8, shield.Health));          // ShieldMaxHealth   (identity)
                    properties.Add(Prop(0x0005C74B, 0));                      // ShieldVolatileHealth
                    properties.Add(Prop(0x0001ECCD, shield.Power));           // SpaceshipShieldPartMaxPower
                    properties.Add(Prop(0x001EE8C9, shield.SysHealth));       // ShipSystemShieldsHealth
                    properties.Add(Prop(0x001EF0CC, shield.SysHealth));       // ShipSystemShieldsEMHealth (identity)
                    properties.Add(Prop(0x0001158A, 1));                      // ShipSystemDamageWeightShield
                    properties.Add(Prop(0x00019080, 0.5f));                   // SpaceshipCrewRating
                    properties.Add(Prop(0x000002D4, 5));                      // Health (generic hull)
                }

                // An engine or a shield also declares its CLASS -- the keyword the ship builder
                // groups and gates on. Vanilla engines and shields carry ShipModuleClass<A|B|C>;
                // a structural part does not, which is why this is added only on those paths.
                var gbfmKeywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>()
                {
                    ShipModuleManufacturerDeimos
                };
                string? moduleClass = engine?.Class ?? shield?.Class;
                if (moduleClass != null)
                {
                    gbfmKeywords.Add(new FormKey(env.LoadOrder[0].ModKey, ShipClassKeyword(moduleClass))
                        .ToLink<IKeywordGetter>());
                    Console.WriteLine("           keyword ShipModuleClass" + moduleClass);
                }

                var gbfm_components = new ExtendedList<AComponent>()
                {
                    new PropertySheetComponent()
                    {
                        Properties = properties
                    },
                    new FormLinkDataComponent()
                    {
                        Links=  new ExtendedList<FormLinkComponentLink>
                        {
                            new FormLinkComponentLink()
                            {
                                LinkedForm = packin.ToNullableLink<IStarfieldMajorRecordGetter>(),
                                Keyword = SpaceshipLinkedExterior,
                            }
                        }
                    },
                    new KeywordFormComponent()
                    {
                        Keywords = gbfmKeywords
                    },
                    new FullNameComponent()
                    {
                        // The string the ship builder shows on the part card. Defaulting this to
                        // <item> is why parts built before --name shipped displaying their
                        // EditorID stub ("eng01") rather than a product name.
                        Name = optName ?? item
                    }
                };
                var gbfm = new GenericBaseForm(myMod)
                {
                    EditorID = prefix + "_gbfm_" + item,
                    ObjectBounds = new ObjectBounds() { First = new P3Float(0, 0, 0), Second = new P3Float(0, 0, 0) },
                    Template = FormSpaceshipModule,
                    Components = gbfm_components,
                };
                myMod.GenericBaseForms.Add(gbfm);

                //Constructable object -------------------------
                Console.WriteLine("Building Record : " + prefix + "_co_" + item);
                IFormLinkNullable<IKeywordGetter> WorkbenchShipBuildingKeyword = new FormKey(env.LoadOrder[0].ModKey, 0x0029C480).ToNullableLink<IKeywordGetter>();

                // The recipe FILTER -- FNAM, the builder-menu category the part shows under.
                // It was declared here before and never attached, so every generated part had
                // no category tab. Default Category_ShipMod_Structure (0x0029C473); --category
                // overrides for non-structural parts (fuel/cargo/engine each carry a different
                // vanilla category, and a mod can pass its own by id).
                IFormLinkGetter<IKeywordGetter> categoryLink;
                if (optCategory != null)
                {
                    if (optCategory.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!uint.TryParse(optCategory.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out var catId))
                        {
                            Console.WriteLine("Error: --category '" + optCategory + "' is not a hex FormID");
                            return 1;
                        }
                        // A bare id is Starfield.esm-relative unless it carries an index byte.
                        var modKey = (catId >> 24) == 0 ? env.LoadOrder[0].ModKey : newMod;
                        categoryLink = new FormKey(modKey, catId & 0x00FFFFFF).ToLink<IKeywordGetter>();
                    }
                    else
                    {
                        var kw = FindKeyword(myMod, env, optCategory);
                        if (kw == null)
                        {
                            Console.WriteLine("Error: no Keyword with EditorID '" + optCategory + "' in " + modname + " or Starfield.esm");
                            return 1;
                        }
                        categoryLink = kw;
                    }
                    Console.WriteLine("Recipe filter : " + optCategory);
                }
                else
                {
                    categoryLink = new FormKey(env.LoadOrder[0].ModKey, 0x0029C473).ToLink<IKeywordGetter>();
                }

                var co = new ConstructibleObject(myMod)
                {
                    EditorID = prefix + "_co_" + item,
                    // Default stays the <item> stub so a missing --desc is VISIBLE in the UI
                    // rather than silently wearing borrowed text; setdesc patches it after.
                    Description = optDesc ?? item,
                    CreatedObject = gbfm.ToNullableLink<IConstructibleObjectTargetGetter>(),
                    AmountProduced = 1,
                    MenuSortOrder = 1,
                    LearnMethod = ConstructibleObject.LearnMethodEnum.DefaultOrConditions,
                    Value = 1000,
                    WorkbenchKeyword = WorkbenchShipBuildingKeyword,
                    RecipeFilters = new ExtendedList<IFormLinkGetter<IKeywordGetter>>() { categoryLink },
                };

                myMod.ConstructibleObjects.Add(co);

                }   // end of the GBFM+COBJ tail (skipped entirely by --mstt-only)

                // Finish up ---------------------------------------------
            }

            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                rec.IsCompressed = false;
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());

            // Two fields every RENDERING part carries have NO property on any Mutagen type, so
            // they cannot be set on the record objects above -- they are spliced into the written
            // file. Without them a part builds, attaches, is buyable and paintable, and DRAWS
            // NOTHING (atsd_panelvent01_port, 2026-07-30 -- the first part ever taken to the glass
            // without a CK save in between; the CK writes both, which is how fourteen parts hid
            // it). Wired here rather than left as a step to remember: the gen_setbounds pattern.
            //
            // --reuse-packin authors no Cell of its own (it shares the donor's), so there is
            // nothing to conform on that path -- the GBFM is still ours, but its cell is the
            // donor's and already correct.
            // --mstt-only authors neither the GBFM nor a Cell, which are the only two records
            // conform touches -- so there is nothing here for it to write, and its
            // "no GenericBaseForm ... nothing written" refusal would be a FALSE alarm rather
            // than a finding. A cloned vanilla record carries both fields already.
            if (optReusePackin == null && !optMsttOnly)
            {
                Console.WriteLine("Conforming (the fields Mutagen cannot author):");
                if (gen_conform.Apply(datapath + "\\" + modname + ".esm", prefix, item, verbose: true) != 0)
                    Console.WriteLine("  !! CONFORM FAILED -- this part will NOT render. Fix before testing.");
            }

            Console.WriteLine("Finished");
            return 0;
        }

        // ShipSnap_SMOD_Generic_1x1x1_All01 -- the canonical six-face cube every authored
        // node here is copied from, so the node record and its rotation come from the game.
        const uint CanonicalCube = 0x00059B01;

        // ---- ENGINE stats -----------------------------------------------------------
        // Ceilings are the BEST vanilla engine in each class, in per-power units, derived
        // from the full 66-engine vanilla set. Crossing one makes your engine the strongest
        // in the game on that axis; crossing two at once is the SAL-6830 failure.
        //   A  7620 thrust / 1610 manoeuvre  (SA-4330, the level-43 class-A king)
        //   B  8860 thrust / 1850 manoeuvre  (Dunn-71 / SAE-5660)
        //   C  9000 thrust / 3900 manoeuvre  (SAL-6330, level 60)
        // This refusal exists because an audit of AvontechShipyards found every over-ceiling
        // engine was HAND-TYPED and every one copied from a vanilla stat block was fine. The
        // defect was a process one, so the fix belongs in the generator.
        sealed class EngineSpec
        {
            public string Class = "A";
            public float Force, Thruster;
            public float Power = 2, ThrusterPower = 4, Health = 64, Speed;

            static readonly Dictionary<string, (float thrust, float manoeuvre, float speed)> Ceiling =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    { "A", (7620f, 1610f, 150f) },
                    { "B", (8860f, 1850f, 140f) },
                    { "C", (9000f, 3900f, 130f) },
                };

            public static EngineSpec? Parse(string spec)
            {
                var e = new EngineSpec();
                bool haveForce = false, haveThruster = false, haveClass = false, haveSpeed = false;
                foreach (var pair in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=');
                    if (kv.Length != 2)
                    {
                        Console.WriteLine("Error: bad --engine term '" + pair + "' -- want key=value");
                        return null;
                    }
                    var k = kv[0].Trim();
                    var v = kv[1].Trim();
                    if (k.Equals("class", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Ceiling.ContainsKey(v))
                        {
                            Console.WriteLine("Error: --engine class must be A, B or C (got '" + v + "')");
                            return null;
                        }
                        e.Class = v.ToUpperInvariant(); haveClass = true; continue;
                    }
                    if (!float.TryParse(v, out var n))
                    {
                        Console.WriteLine("Error: --engine " + k + " wants a number (got '" + v + "')");
                        return null;
                    }
                    switch (k.ToLowerInvariant())
                    {
                        case "force":    e.Force = n; haveForce = true; break;
                        case "thruster": e.Thruster = n; haveThruster = true; break;
                        case "power":    e.Power = n; break;
                        case "tpower":   e.ThrusterPower = n; break;
                        case "health":   e.Health = n; break;
                        case "speed":    e.Speed = n; haveSpeed = true; break;
                        default:
                            Console.WriteLine("Error: unknown --engine key '" + k
                                + "'. Keys: class force thruster power tpower health speed");
                            return null;
                    }
                }
                if (!haveClass || !haveForce || !haveThruster)
                {
                    Console.WriteLine("Error: --engine needs at least class, force and thruster");
                    return null;
                }
                var cap = Ceiling[e.Class];
                if (!haveSpeed) e.Speed = cap.speed;

                // The refusal. Report BOTH axes before returning so one run names every problem.
                bool over = false;
                if (e.Force > cap.thrust)
                {
                    Console.WriteLine($"REFUSED: force {e.Force}/pwr exceeds the class-{e.Class} vanilla ceiling {cap.thrust}"
                        + $" ({e.Force / cap.thrust:0.00}x). Total at the 12-power cap would be {e.Force * 12:N0}"
                        + $" vs the best vanilla {cap.thrust * 12:N0}.");
                    over = true;
                }
                if (e.Thruster > cap.manoeuvre)
                {
                    Console.WriteLine($"REFUSED: thruster {e.Thruster}/pwr exceeds the class-{e.Class} vanilla ceiling {cap.manoeuvre}"
                        + $" ({e.Thruster / cap.manoeuvre:0.00}x).");
                    over = true;
                }
                if (e.Speed > cap.speed)
                {
                    Console.WriteLine($"REFUSED: speed {e.Speed} exceeds the class-{e.Class} standard {cap.speed}."
                        + " Only White Dwarf 3015 goes above its class (180), and it pays with HALF the class's health.");
                    over = true;
                }
                if (over)
                {
                    Console.WriteLine("  (These are the vanilla maxima, not a style guide. If you mean it, raise the"
                        + " ceiling here in one place rather than passing a bigger number.)");
                    return null;
                }
                Console.WriteLine($"Engine   : class {e.Class}, {e.Force}/pwr thrust x {e.Power} pwr = {e.Force * e.Power:N0} shown"
                    + $"; {e.Thruster}/pwr manoeuvre = {e.Thruster * e.Power:N0} shown; speed {e.Speed}");
                Console.WriteLine($"           at the 12-power cap: thrust {e.Force * 12:N0} ({e.Force / cap.thrust:P0} of the"
                    + $" class-{e.Class} ceiling), manoeuvre {e.Thruster * 12:N0} ({e.Thruster / cap.manoeuvre:P0})");
                return e;
            }

        }

        // ShipModuleClass<A|B|C>, Starfield.esm. Lifted out of EngineSpec when the shield path
        // became the second caller: the map is a CLOSED three-entry constant, so two copies would
        // not have been a bug farm, but reactor and grav drive will each want it and the third
        // copy is the tell. One call site each, pure function, nothing else moved.
        static uint ShipClassKeyword(string cls) => cls switch
        {
            "A" => 0x0026FE57,
            "B" => 0x0026FE56,
            _   => 0x0026FE55,
        };

        // A shield's stat block. Read off the vanilla corpus (2026-09-03) via
        // `gen_inspect genericbaseform _Shields_` -- 112 records, of which 91 are player ship
        // modules in classes A/B/C (the rest are the SMM_ set, which carries no level gate and no
        // recipe, plus four from a Creation).
        //
        // ⭐ THE CLASS IS MOST OF THE DESIGN, exactly as it is for engines. Regen, non-combat
        // regen, crew rating, hull health, volatile health and damage weight have ZERO within-class
        // variation across the 90 purchasable records, so they are constants keyed on the class
        // rather than parameters.
        // What is left is ShieldHealth (the dial), the mass that pays for it, the power slots and
        // the module's own system health.
        //
        // ⛔ MASS IS THE DENOMINATOR HERE TOO. A shield that is light for its health is a stealth
        // buff to the whole ship, and a health-only audit under-reports it -- the same error the
        // Shipyards engine audit made and had to correct. So the refusal grades health/mass against
        // the class's own measured maximum, not health alone.
        sealed class ShieldSpec
        {
            public string Class = "A";
            public float Health, Mass;
            public float Power = 3;
            public float SysHealth = -1;             // -1 = derive from health (see Parse)

            public float RegenRate          => Class == "A" ? 0.1f  : Class == "B" ? 0.08f : 0.05f;
            public float RegenRateNonCombat => Class == "C" ? 0.15f : 0.2f;

            // Per class: the ceilings vanilla itself never crosses, plus the ENTRY rung, which is
            // where an ungated part should be sitting. Measured over the PURCHASABLE set -- the
            // levelled ladder plus the rare Experimentals, which are real vendor stock.
            //   A: 51 records, health 310..860,  health/mass max 18.10, sysHP <= 208
            //   B: 20 records, health 505..1500, health/mass max 16.67, sysHP <= 438
            //   C: 19 records, health 680..1600, health/mass max 11.34, sysHP <= 660
            //
            // ⛔ ONE RECORD IS EXCLUDED AND IT IS THE ONLY EXCLUSION: SMB_Shields_Vanguard_Bulwark_UC01,
            // a UC Vanguard quest reward that is not sold anywhere. It is the outlier on BOTH axes
            // that matter here -- health/mass 20.71 against a purchasable class-B max of 16.67, and
            // ShieldRegenRate 0.065 where the other twenty class-B records all carry 0.08. Leaving
            // it in would have set the class-B ratio cap 24% above anything a player can buy, which
            // is raising the cap to fit the subject, and it would have made the "no within-class
            // variation" claim above FALSE by exactly one record. With it out, all six constants are
            // uniform in all three classes -- so the exclusion is what makes the class-constant
            // design true, not a convenience. (I first wrote this table with 20.71 in it and with a
            // guess about the regen split that was wrong in both directions. Measured, then fixed.)
            static readonly Dictionary<string, (float health, float ratio, float sysHealth, float entry)> Ceiling =
                new(StringComparer.OrdinalIgnoreCase)
                {
                    { "A", (860f,  18.10f, 208f, 310f) },
                    { "B", (1500f, 16.67f, 438f, 505f) },
                    { "C", (1600f, 11.34f, 660f, 680f) },
                };

            public static ShieldSpec? Parse(string spec)
            {
                var s = new ShieldSpec();
                bool haveClass = false, haveHealth = false, haveMass = false;
                foreach (var pair in spec.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var kv = pair.Split('=');
                    if (kv.Length != 2)
                    {
                        Console.WriteLine("Error: bad --shield term '" + pair + "' -- want key=value");
                        return null;
                    }
                    var k = kv[0].Trim();
                    var v = kv[1].Trim();
                    if (k.Equals("class", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!Ceiling.ContainsKey(v))
                        {
                            Console.WriteLine("Error: --shield class must be A, B or C (got '" + v + "')");
                            return null;
                        }
                        s.Class = v.ToUpperInvariant(); haveClass = true; continue;
                    }
                    if (!float.TryParse(v, out var n))
                    {
                        Console.WriteLine("Error: --shield " + k + " wants a number (got '" + v + "')");
                        return null;
                    }
                    switch (k.ToLowerInvariant())
                    {
                        case "health":    s.Health = n; haveHealth = true; break;
                        case "mass":      s.Mass = n; haveMass = true; break;
                        case "power":     s.Power = n; break;
                        case "syshealth": s.SysHealth = n; break;
                        default:
                            Console.WriteLine("Error: unknown --shield key '" + k
                                + "'. Keys: class health mass power syshealth");
                            return null;
                    }
                }
                if (!haveClass || !haveHealth || !haveMass)
                {
                    Console.WriteLine("Error: --shield needs at least class, health and mass");
                    return null;
                }
                if (s.Health <= 0 || s.Mass <= 0 || s.Power <= 0)
                {
                    // A zero anywhere here is not a small shield. Zero health is a shield that
                    // absorbs nothing while the card still reads like a shield; zero mass is the
                    // stealth buff in its purest form; zero power is a module the builder's
                    // arithmetic reads as absent. Same refusal as --cargo's zero capacity.
                    Console.WriteLine($"Error: --shield health/mass/power must all be positive"
                        + $" (got health {s.Health}, mass {s.Mass}, power {s.Power})");
                    return null;
                }

                var cap = Ceiling[s.Class];

                // ShipSystemShieldsHealth is the module's OWN durability, not the bubble's. It
                // runs 0.095-0.277 of ShieldHealth across class A with no tighter law, so the
                // default is the middle of that band rather than a derived truth -- and it is a
                // default precisely because leaving it absent would ship a shield the game reads
                // as having no module health at all.
                if (s.SysHealth < 0) s.SysHealth = MathF.Round(s.Health / 6f);
                if (s.SysHealth <= 0)
                {
                    Console.WriteLine($"Error: --shield syshealth must be positive (got {s.SysHealth})");
                    return null;
                }

                // The refusal. Report EVERY axis before returning, so one run names every problem
                // rather than sending the author round again for the next one.
                bool over = false;
                if (s.Health > cap.health)
                {
                    Console.WriteLine($"REFUSED: health {s.Health} exceeds the class-{s.Class} vanilla ceiling"
                        + $" {cap.health} ({s.Health / cap.health:0.00}x).");
                    over = true;
                }
                float ratio = s.Health / s.Mass;
                if (ratio > cap.ratio)
                {
                    Console.WriteLine($"REFUSED: health/mass {ratio:0.00} exceeds the class-{s.Class} vanilla"
                        + $" maximum {cap.ratio:0.00}. Mass is the brake -- under-massing a shield is a"
                        + $" stealth buff to the whole ship, so this refuses at {s.Health}/{s.Mass}"
                        + $" even though the health alone is in range.");
                    over = true;
                }
                if (s.SysHealth > cap.sysHealth)
                {
                    Console.WriteLine($"REFUSED: syshealth {s.SysHealth} exceeds the class-{s.Class} vanilla"
                        + $" ceiling {cap.sysHealth}.");
                    over = true;
                }
                if (s.Power > 12)
                {
                    Console.WriteLine($"REFUSED: power {s.Power} exceeds the 12-slot bar. No vanilla module"
                        + " of any class goes above it, and the bar itself caps there.");
                    over = true;
                }
                if (over)
                {
                    Console.WriteLine("  (These are the vanilla maxima, not a style guide. If you mean it,"
                        + " raise the ceiling here in one place rather than passing a bigger number.)");
                    return null;
                }

                Console.WriteLine($"Shield   : class {s.Class}, health {s.Health} at mass {s.Mass}"
                    + $" (health/mass {ratio:0.00}, class max {cap.ratio:0.00}), power {s.Power},"
                    + $" system health {s.SysHealth}");
                Console.WriteLine($"           regen {s.RegenRate}/s combat, {s.RegenRateNonCombat}/s out of"
                    + $" combat -- class constants, not authored here");
                Console.WriteLine($"           {s.Health / cap.health:P0} of the class-{s.Class} ceiling"
                    + $" ({cap.health}); the class ENTRY rung is {cap.entry}."
                    + (s.Health <= cap.entry
                        ? "  <- entry tier, safe to ship UNGATED"
                        : "  <- ABOVE the entry rung: gate it with `setcondition`, or it is"
                          + " strictly better than the bottom of the ladder at level 1"));
                return s;
            }
        }

        static readonly Dictionary<string, uint> SnapFaceNodes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            { "Fore", 0x0004AB6F },   // structural faces -- these six live on the canonical cube,
                                      // so their rotation is copied from it (rotation is a property
                                      // of the face). Everything in SnapExtraNodes below does not.
            { "Aft", 0x0004AB70 },
            { "Port", 0x0004AB73 },
            { "Starboard", 0x0004AB74 },
            { "Top", 0x0004AB77 },
            { "Bottom", 0x0004AB78 },
        };

        // Nodes that are NOT structural faces and NOT on the canonical cube -- chiefly the
        // EQUIPMENT mounts, which is where a weapon attaches. A survey of the load order's
        // SnapTemplates finds 59 distinct node forms, not the six above; the six merely happen
        // to be the ones a cube has. SHIP_Equipment_Side01A is the 4th most-used node in the
        // whole game (538 uses) and was invisible until gen_inspect stopped printing "?" for
        // every form outside its six-name table.
        //
        // Starfield.esm ONLY. SFBGS050_SHIP_Equipment_FrontBack01A exists but lives in an update
        // master, and a paid Creation may not depend on one -- so it is deliberately absent.
        //
        // Rotation on these is NOT derivable from a face: the same form appears with several
        // rotations depending on which way the mounted equipment should point. Default is the
        // most common (270,0,0); pass an explicit rotation as a 4th spec field when it matters.
        static readonly Dictionary<string, uint> SnapExtraNodes = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
        {
            { "EquipSide", 0x0004AB85 },        // SnapNode_SHIP_Equipment_Side01A  -- the weapon/equipment mount
            { "EquipSideB", 0x0004AB89 },       // SnapNode_SHIP_Equipment_Side01B  -- rare variant (2 uses)
            { "GenericSide", 0x0004AB76 },      // SnapNode_SHIP_GenericSide01
            { "GenericForeAft", 0x00294D0B },   // SnapNode_SHIP_GenericForeAft01
        };

        static readonly P3Float DefaultExtraRotation = new P3Float(270, 0, 0);

        static SnapTemplate? FindSnapTemplate(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string editorId)
        {
            foreach (var st in myMod.SnapTemplates)
                if (string.Equals(st.EditorID, editorId, StringComparison.OrdinalIgnoreCase)) return st;
            foreach (var st in env.LoadOrder[0].Mod!.SnapTemplates)
                if (string.Equals(st.EditorID, editorId, StringComparison.OrdinalIgnoreCase)) return st.DeepCopy();
            return null;
        }

        static IFormLinkGetter<IKeywordGetter>? FindKeyword(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string editorId)
        {
            foreach (var kw in myMod.Keywords)
                if (string.Equals(kw.EditorID, editorId, StringComparison.OrdinalIgnoreCase))
                    return kw.ToLink<IKeywordGetter>();
            foreach (var kw in env.LoadOrder[0].Mod!.Keywords)
                if (string.Equals(kw.EditorID, editorId, StringComparison.OrdinalIgnoreCase))
                    return kw.ToLink<IKeywordGetter>();
            return null;
        }

        static IFormLinkGetter<ILayeredMaterialSwapGetter>? FindMaterialSwap(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string editorId)
        {
            foreach (var sw in myMod.LayeredMaterialSwaps)
                if (string.Equals(sw.EditorID, editorId, StringComparison.OrdinalIgnoreCase))
                    return sw.ToLink<ILayeredMaterialSwapGetter>();
            foreach (var sw in env.LoadOrder[0].Mod!.LayeredMaterialSwaps)
                if (string.Equals(sw.EditorID, editorId, StringComparison.OrdinalIgnoreCase))
                    return sw.ToLink<ILayeredMaterialSwapGetter>();
            return null;
        }

        // "Starboard@-4,0,0;Aft@0,-3.65,0" -> a SnapTemplate carrying those faces, each node
        // lifted verbatim off the vanilla cube with only its Offset moved.
        internal static SnapTemplate? BuildSnapTemplate(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string editorId, string spec)
        {
            SnapTemplate? canonical = null;
            foreach (var st in env.LoadOrder[0].Mod!.SnapTemplates)
                if (st.FormKey.ID == CanonicalCube) canonical = st.DeepCopy();
            if (canonical == null)
            {
                Console.WriteLine("Error: could not read vanilla ShipSnap_SMOD_Generic_1x1x1_All01 to copy nodes from");
                return null;
            }

            var template = new SnapTemplate(myMod)
            {
                EditorID = editorId,
                NextNodeID = canonical.NextNodeID,
                STPT = canonical.STPT,
            };

            foreach (var entry in spec.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                // Face@x,y,z            offset only; rotation comes from the face (structural)
                // Node@x,y,z@rx,ry,rz   explicit rotation (equipment nodes, where rotation is a
                //                       property of where the mounted thing should point, not of
                //                       the host face)
                var bits = entry.Split('@');
                if (bits.Length != 2 && bits.Length != 3)
                {
                    Console.WriteLine("Error: bad node spec '" + entry + "' -- want Face@x,y,z or Node@x,y,z@rx,ry,rz");
                    return null;
                }
                var face = bits[0].Trim();
                bool isExtra = false;
                if (!SnapFaceNodes.TryGetValue(face, out var nodeId))
                {
                    if (!SnapExtraNodes.TryGetValue(face, out nodeId))
                    {
                        Console.WriteLine("Error: unknown node '" + face + "'. Faces: "
                            + string.Join(" ", SnapFaceNodes.Keys) + " | Equipment/other: "
                            + string.Join(" ", SnapExtraNodes.Keys));
                        return null;
                    }
                    isExtra = true;
                }
                var nums = bits[1].Split(',');
                if (nums.Length != 3
                    || !float.TryParse(nums[0], out var ox)
                    || !float.TryParse(nums[1], out var oy)
                    || !float.TryParse(nums[2], out var oz))
                {
                    Console.WriteLine("Error: bad offset in '" + entry + "' -- want three numbers");
                    return null;
                }

                P3Float? explicitRot = null;
                if (bits.Length == 3)
                {
                    var r = bits[2].Split(',');
                    if (r.Length != 3
                        || !float.TryParse(r[0], out var rx)
                        || !float.TryParse(r[1], out var ry)
                        || !float.TryParse(r[2], out var rz))
                    {
                        Console.WriteLine("Error: bad rotation in '" + entry + "' -- want three numbers");
                        return null;
                    }
                    explicitRot = new P3Float(rx, ry, rz);
                }

                if (isExtra)
                {
                    // Not on the cube, so there is nothing to copy: build the entry outright and
                    // allocate a NodeID the template is not already using.
                    var link = new FormKey(env.LoadOrder[0].ModKey, nodeId).ToLink<ISnapTemplateNodeGetter>();
                    uint nextId = template.NextNodeID ?? 0;
                    foreach (var n in template.Nodes) if (n.NodeID >= nextId) nextId = n.NodeID + 1;
                    template.Nodes.Add(new SnapNodeEntry()
                    {
                        Node = link,
                        NodeID = nextId,
                        Rotation = explicitRot ?? DefaultExtraRotation,
                        Offset = new P3Float(ox, oy, oz),
                    });
                    template.NextNodeID = nextId + 1;
                    continue;
                }

                SnapNodeEntry? source = null;
                foreach (var n in canonical.Nodes)
                    if (n.Node.FormKey.ID == nodeId) source = n;
                if (source == null)
                {
                    Console.WriteLine("Error: the vanilla cube carries no " + face + " node");
                    return null;
                }

                // A part bigger than one cell carries a node PER CELL PER FACE -- vanilla's 2x1
                // hab ShipSnap_SMOD_Hab_Nova_LH-2L ships two Port, two Starboard, two Top and
                // two Bottom, each with its OWN NodeID. So the same face may legitimately be
                // given more than once, and only the first occurrence can keep the cube's id.
                // The first keeping it is deliberate: a single-cell part regenerates unchanged.
                uint chosenId = source.NodeID;
                bool idTaken = false;
                foreach (var n in template.Nodes) if (n.NodeID == chosenId) idTaken = true;
                if (idTaken)
                {
                    uint nextId = template.NextNodeID ?? 0;
                    foreach (var n in template.Nodes) if (n.NodeID >= nextId) nextId = n.NodeID + 1;
                    chosenId = nextId;
                    template.NextNodeID = nextId + 1;
                }

                template.Nodes.Add(new SnapNodeEntry()
                {
                    Node = source.Node,
                    NodeID = chosenId,
                    // A structural face's rotation IS the face, so an explicit one is almost
                    // certainly a mistake -- but honour it rather than silently ignore it.
                    Rotation = explicitRot ?? source.Rotation,
                    Offset = new P3Float(ox, oy, oz),
                });
            }

            if (template.Nodes.Count == 0)
            {
                Console.WriteLine("Error: --snap-nodes produced no nodes");
                return null;
            }

            // Print what was authored, face#id. A duplicate NodeID is invisible in the record and
            // in the builder -- it silently cost every multi-cell part until 2026-08-12 -- so the
            // ids are SHOWN rather than assumed, and a repeated face is legible at authoring time.
            var faceOf = new Dictionary<uint, string>();
            foreach (var kv in SnapFaceNodes) faceOf[kv.Value] = kv.Key;
            foreach (var kv in SnapExtraNodes) faceOf[kv.Value] = kv.Key;
            Console.WriteLine("  nodes: " + string.Join("  ", template.Nodes.Select(n =>
                (faceOf.TryGetValue(n.Node.FormKey.ID, out var nm) ? nm : n.Node.FormKey.ID.ToString("X6"))
                + "#" + n.NodeID)));

            return template;
        }
    }
}
