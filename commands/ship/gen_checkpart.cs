using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    /// <summary>
    /// The RECORD half of the part doctor (the asset half is check_part.py in the mod). Emits
    /// the plugin facts a ship part must satisfy -- as raw JSON, never a verdict. It is the ONLY
    /// honest oracle for these facts (they live in the plugin, readable only through Mutagen):
    ///
    ///   * the MoveableStatic exists, its Model points at the part's NIF
    ///   * the Model carries the recolour flag ("Support Model Only Swap" / HasFirstPersonModel)
    ///     -- the enabler whose absence renders + attaches a part but offers NO paint option
    ///     (2026-07-22, the wing recolour hunt)
    ///   * material swaps are wired onto the Model and resolve
    ///   * the build chain links MSTT -> PKIN -> GBFM -> COBJ
    ///   * the plugin's master type
    ///
    /// It prints one line, `CHECKPART_JSON {...}`, so check_part.py can grep it out of the dotnet
    /// build noise. ALL pass/fail judgement lives in check_part.py -- this only reports what IS.
    ///
    /// Invoked (via RunLegacy): dotnet run -- checkpart &lt;modname&gt; &lt;item&gt;
    ///   arr = [modname, "checkpart", item]; the MSTT is found by its "_ms_&lt;item&gt;" suffix so
    ///   the &lt;prefix&gt; need not be passed -- it is read off the record and the chain derived from it.
    ///
    /// BATCH MODE (2026-09-15). &lt;item&gt; may instead be a sentinel:
    ///   @all            every MoveableStatic in the plugin whose EditorID contains "_ms_"
    ///   @items:a,b,c    exactly these items, so the CALLER owns what the corpus is
    /// One CHECKPART_JSON line is emitted per item, from ONE Mutagen load. The single-part path
    /// is unchanged and both go through the same InspectPart body, so a batch row and a separate
    /// invocation of the same item are the same bytes.
    ///
    /// ⚠ WHY A SENTINEL AND NOT "does it contain a comma". `@` cannot occur in a Bethesda
    /// EditorID, so a typo refuses instead of silently becoming a different request -- the same
    /// argument banked for `setlinks @null`. An implicit rule would make a malformed part name
    /// into a batch request and print nothing anybody asked for.
    ///
    /// ⛔ WHY IT EXISTS. A per-item checker only speaks when asked, so a corpus with no sweep has
    /// no alarm -- and at ~35s of Mutagen load per invocation a 94-part sweep was 55 minutes, which
    /// is why the only one ever run (2026-09-03) was a shell loop in a scratch session whose sole
    /// committed artifact was its output. Batched it is one load.
    /// </summary>
    public class gen_checkpart
    {
        public static int Generate(string[] args)
        {
            string modname = args[0];
            string item = args[2];

            try
            {
                using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
                var cache = env.LinkCache;

                IStarfieldModGetter? mod = null;
                foreach (var listing in env.LoadOrder.ListedOrder)
                    if (listing.Mod != null && string.Equals(listing.ModKey.Name, modname, StringComparison.OrdinalIgnoreCase))
                    { mod = listing.Mod; break; }

                if (mod == null)
                    return Emit(new Dictionary<string, object?>
                    {
                        ["modname"] = modname,
                        ["part"] = item,
                        ["ok"] = false,
                        ["error"] = $"{modname}.esm is not in the active load order -- enable it, or it can't be audited",
                    });

                // master type -- authored as ModType.Master; report what the header actually says
                var hflags = mod.ModHeader.Flags;
                string ft = hflags.ToString();
                bool isMaster = ft.Contains("Master", StringComparison.OrdinalIgnoreCase);
                bool isSmall = ft.Contains("Small", StringComparison.OrdinalIgnoreCase) || ft.Contains("Light", StringComparison.OrdinalIgnoreCase);
                string masterType = isMaster ? (isSmall ? "Small" : "Full") : "None";

                // ================= THE CHAIN IS A GRAPH OF FORMLINKS, SO WALK IT =================
                //
                // ⛔ WHAT THIS REPLACED, 2026-09-15, and the defect is worth stating because the
                // argument against it was ALREADY IN THIS FILE, applied to exactly one leg. Until
                // today every chain hop was found by COMPOSING A STRING -- prefix + "_pkn_" + item,
                // then "_gbfm_", "_co_", "_sntp_" -- while the setCobj leg twenty lines below found
                // its COBJ by walking a FormList for the GBFM's FormKey, with a comment reading
                // "the real invariant; a name suffix is only a marker". It was right, and it was
                // never carried to its neighbours.
                //
                // The cost was measured rather than argued: EVERY generator-named part resolved and
                // BOTH parts a human named (atsd_hab_01, atsd_bay_01) reported their whole chain
                // ABSENT while working in game. atsd_bay_01 misses on three axes at once (_pkm_ for
                // _pkn_, bay01 for bay_01, atsd_SMS_Bay_01 for atsd_gbfm_bay_01) and a bay carries
                // TWO PackIns where the composed rule can only spell one. Discovery was keyed on the
                // one thing a human can spell differently, with the authoritative link sitting there.
                //
                // The walk, each hop a real link:
                //   MSTT  -> the CELL that PLACES it   (a PlacedObject whose Base is the MSTT)
                //   CELL  -> the PACKIN that owns it   (PackIn.Cell)
                //   PKIN  -> the GBFM that LINKS it    (FormLinkData -> SpaceshipLinkedExterior)
                //   GBFM  -> the COBJ that CREATES it  (COBJ.CreatedObject)
                //   MSTT  -> its SNAPTEMPLATE          (MoveableStatic.SnapTemplate)
                //
                // The naming convention is NOT discarded -- it is demoted from the discovery
                // MECHANISM to a reported FACT (`expectedEditorId` + `nameMatchesConvention` per
                // node), so a misnamed part is still called out by check_part.py instead of
                // vanishing. A convention nothing can see is not a convention; a convention doing
                // load-bearing lookup work is a trap.
                var allCells = mod.Cells.Records.SelectMany(b => b.SubBlocks).SelectMany(sb => sb.Cells).ToList();

                // base FormKey -> every cell that places it. A LIST, not a last-writer-wins slot:
                // a base placed in two cells is a real possibility and silently picking one of them
                // is how a lookup starts lying.
                var cellsPlacing = new Dictionary<FormKey, List<FormKey>>();
                foreach (var c in allCells)
                    foreach (var entry in c.Persistent.Concat(c.Temporary))
                        if (entry is IPlacedObjectGetter po && !po.Base.IsNull)
                        {
                            if (!cellsPlacing.TryGetValue(po.Base.FormKey, out var lst))
                                cellsPlacing[po.Base.FormKey] = lst = new List<FormKey>();
                            if (!lst.Contains(c.FormKey)) lst.Add(c.FormKey);
                        }

                // PackIn linked-form FormKey -> every GBFM that links it. A bay's GBFM carries TWO
                // links (exterior AND interior), so both keys map to it and whichever PackIn we
                // arrived through resolves the same GBFM.
                //
                // ⛔ A LIST, AND THIS ONE IS A SCAR RATHER THAN FORESIGHT. The first version of this
                // index was a last-writer-wins slot, written one block after the comment above
                // refusing exactly that for cells. The corpus sweep caught it: TWO GBFMs legitimately
                // share one PackIn on this line -- atsd_gbfm_cargoinline_01 and its DECORATIVE TWIN
                // atsd_gbfm_decocargo_01 -- so cargoinline_01 silently resolved to the decoration.
                // A single-slot index does not fail on an ambiguity, it PICKS, and the pick reads
                // exactly like a resolution.
                var gbfmsByLinkedForm = new Dictionary<FormKey, List<IGenericBaseFormGetter>>();
                foreach (var g in mod.GenericBaseForms)
                    foreach (var comp in g.Components)
                        if (comp is IFormLinkDataComponentGetter fld)
                            foreach (var l in fld.Links)
                                if (!l.LinkedForm.IsNull)
                                {
                                    if (!gbfmsByLinkedForm.TryGetValue(l.LinkedForm.FormKey, out var lst))
                                        gbfmsByLinkedForm[l.LinkedForm.FormKey] = lst = new List<IGenericBaseFormGetter>();
                                    if (!lst.Any(x => x.FormKey == g.FormKey)) lst.Add(g);
                                }

                // The per-part body, as a local function so it captures `mod` and `cache` without
                // naming their generic types. Single and batch call THIS and nothing else.
                Dictionary<string, object?> InspectPart(string part)
                {
                    var outp = new Dictionary<string, object?> { ["modname"] = modname, ["part"] = part };
                    outp["masterType"] = masterType;
                    outp["masterFlags"] = ft;

                    // MoveableStatic -- found by the "_ms_<item>" suffix so <prefix> is read, not passed
                    string msttSuffix = "_ms_" + part;
                    var mstt = mod.MoveableStatics.FirstOrDefault(
                        m => m.EditorID != null && m.EditorID.EndsWith(msttSuffix, StringComparison.OrdinalIgnoreCase));
                    var msttOut = new Dictionary<string, object?>();
                    outp["mstt"] = msttOut;
                    if (mstt == null)
                    {
                        msttOut["found"] = false;
                        outp["ok"] = true;                 // ran fine; the part just isn't there
                        return outp;
                    }
                    msttOut["found"] = true;
                    msttOut["editorId"] = mstt.EditorID;
                    msttOut["formKey"] = mstt.FormKey.ToString();
                    string prefix = mstt.EditorID!.Substring(0, mstt.EditorID.Length - msttSuffix.Length);

                    var model = mstt.Model;
                    msttOut["modelFile"] = model?.File?.GivenPath;
                    string mflags = model?.Flags.ToString() ?? "";
                    msttOut["modelFlags"] = mflags;
                    msttOut["hasRecolourFlag"] = model?.Flags != null && model.Flags.Value.HasFlag(Model.Flag.HasFirstPersonModel);
                    // LightLayer (FLLD). Absent => the part builds, attaches, flips and paints, and
                    // draws NOTHING. Reported as a raw fact (null when absent); the Python judges it.
                    msttOut["lightLayer"] = model?.LightLayer;
                    var mob = mstt.ObjectBounds;
                    msttOut["objectBounds"] = new[] { mob.First.X, mob.First.Y, mob.First.Z,
                                                      mob.Second.X, mob.Second.Y, mob.Second.Z };

                    var swapsOut = new List<object?>();
                    if (model?.MaterialSwaps != null)
                        foreach (var sw in model.MaterialSwaps)
                        {
                            bool resolved = cache.TryResolveIdentifier(sw.FormKey, out var edid);
                            swapsOut.Add(new Dictionary<string, object?>
                            {
                                ["formKey"] = sw.FormKey.ToString(),
                                ["editorId"] = edid,
                                ["resolved"] = resolved,
                            });
                        }
                    msttOut["materialSwaps"] = swapsOut;

                    // chain: MSTT -> CELL -> PKIN -> GBFM -> COBJ, walked by FormLink (see the
                    // index built above). The composed name rides along as a reported FACT only.
                    var chain = new Dictionary<string, object?>();
                    outp["chain"] = chain;

                    var placingCells = cellsPlacing.TryGetValue(mstt.FormKey, out var pcs) ? pcs : new List<FormKey>();
                    var pkin = placingCells.Count == 0 ? null
                        : mod.PackIns.FirstOrDefault(p => !p.Cell.IsNull && placingCells.Contains(p.Cell.FormKey));
                    string? cellRef = null;
                    if (pkin != null && !pkin.Cell.IsNull) cache.TryResolveIdentifier(pkin.Cell.FormKey, out cellRef);
                    chain["pkin"] = Node(pkin?.EditorID, pkin?.FormKey,
                        ("cellRef", cellRef),
                        ("expectedEditorId", prefix + "_pkn_" + part),
                        ("nameMatchesConvention", Eid(pkin?.EditorID, prefix + "_pkn_" + part)),
                        // >1 means the shell is placed in more than one PackIn cell. Reported, never
                        // resolved here: which one is canonical is a judgement, and this is an oracle.
                        ("placingCellCount", placingCells.Count));

                    // The PackIn's OBND and its storage CELL's lighting. Both were silently wrong on
                    // every generated part until 2026-07-30 (the rear vent, invisible in the builder):
                    // the PackIn's bounds were hardcoded to the 1x1x1 grid box, and the cell carried no
                    // LTMP and zeroes where a working cell has FarHeightRange 10000 and a trailing 3.
                    // Nothing noticed because the CK rewrites all of it on save. Raw facts only.
                    if (pkin != null)
                    {
                        var ob = pkin.ObjectBounds;
                        outp["packInBounds"] = new[] { ob.First.X, ob.First.Y, ob.First.Z,
                                                       ob.Second.X, ob.Second.Y, ob.Second.Z };
                    }
                    var cell = pkin != null && !pkin.Cell.IsNull
                        ? allCells.FirstOrDefault(c => c.FormKey == pkin.Cell.FormKey)
                        : null;
                    if (cell != null)
                    {
                        var lit = cell.Lighting;
                        outp["cell"] = new Dictionary<string, object?>
                        {
                            ["editorID"] = cell.EditorID,
                            ["hasLightingTemplate"] = cell.LightingTemplate != null,
                            ["nearHeightRange"] = lit?.NearHeightRange,
                            ["farHeightRange"] = lit?.FarHeightRange,
                            ["unknown1"] = lit?.Unknown1,
                            // XCLL's LAST word (a 3 on every working cell, 0 on every generated one)
                            // has NO property on CellLighting -- Mutagen round-trips it but exposes no
                            // name for it. Opaque to the record model, not to the bytes: check_part.py
                            // reads it off the plugin directly, the same move the swap-mapping leg makes.
                        };
                    }

                    // GBFM: the one that LINKS the PackIn we arrived through.
                    //
                    // The links narrow it to a SET; where that set has more than one member the
                    // CONVENTION NAME is the documented tie-break, and the count is reported so the
                    // ambiguity is visible rather than resolved in silence. That is not discovery by
                    // name: a name that matches nothing in the link set is still ignored.
                    IGenericBaseFormGetter? gbfm = null;
                    int gbfmCandidates = 0;
                    if (pkin != null && gbfmsByLinkedForm.TryGetValue(pkin.FormKey, out var gcands))
                    {
                        gbfmCandidates = gcands.Count;
                        gbfm = gcands.Count == 1
                            ? gcands[0]
                            : gcands.FirstOrDefault(g => Eid(g.EditorID, prefix + "_gbfm_" + part)) ?? gcands[0];
                    }

                    // ⛔ packInRef USED TO KEEP ONLY THE LAST LINK, and a bay carries TWO
                    // (SpaceshipLinkedExterior AND SpaceshipLinkedInterior). The old loop overwrote,
                    // so on any two-PackIn part it reported the INTERIOR one and check_part.py's
                    // "GBFM points at PackIn X, expected Y" leg would have FAILED a correct bay. It
                    // never fired because no part had two links until the first bay. Both are now
                    // reported, and `packInRef` means the one this chain was DISCOVERED THROUGH,
                    // which is a defined thing rather than whichever came last.
                    var packInRefs = new List<object?>();
                    string? packInRef = null;
                    if (gbfm != null)
                        foreach (var comp in gbfm.Components)
                            if (comp is IFormLinkDataComponentGetter fld)
                                foreach (var l in fld.Links)
                                    if (!l.LinkedForm.IsNull)
                                    {
                                        cache.TryResolveIdentifier(l.LinkedForm.FormKey, out var r);
                                        packInRefs.Add(new Dictionary<string, object?>
                                        {
                                            ["editorId"] = r,
                                            ["formKey"] = l.LinkedForm.FormKey.ToString(),
                                        });
                                        if (pkin != null && l.LinkedForm.FormKey == pkin.FormKey) packInRef = r;
                                    }
                    chain["gbfm"] = Node(gbfm?.EditorID, gbfm?.FormKey,
                        ("packInRef", packInRef),
                        ("packInRefs", packInRefs),
                        ("expectedEditorId", prefix + "_gbfm_" + part),
                        ("nameMatchesConvention", Eid(gbfm?.EditorID, prefix + "_gbfm_" + part)),
                        // >1 means several GBFMs link this PackIn (the decorative-twin shape). The
                        // convention name broke the tie; check_part.py should say so out loud.
                        ("gbfmCandidateCount", gbfmCandidates));

                    // COBJ: the one that CREATES this GBFM.
                    var cobj = gbfm == null ? null
                        : mod.ConstructibleObjects.FirstOrDefault(
                            c => !c.CreatedObject.IsNull && c.CreatedObject.FormKey == gbfm.FormKey);
                    string? createdRef = null;
                    if (cobj != null && !cobj.CreatedObject.IsNull) cache.TryResolveIdentifier(cobj.CreatedObject.FormKey, out createdRef);
                    chain["cobj"] = Node(cobj?.EditorID, cobj?.FormKey,
                        ("createdObjectRef", createdRef),
                        ("expectedEditorId", prefix + "_co_" + part),
                        ("nameMatchesConvention", Eid(cobj?.EditorID, prefix + "_co_" + part)));

                    // set-part: a family regrouped into a flip SET (setflipset) has NO per-part COBJ --
                    // one COBJ creates a FormList that CONTAINS this part's GBFM. Report the membership
                    // fact (the real invariant; a name suffix is only a marker); judgement stays in Python.
                    string? setCobjEid = null; FormKey? setCobjKey = null; string? setFlst = null;
                    if (gbfm != null)
                        foreach (var c in mod.ConstructibleObjects)
                        {
                            if (c.CreatedObject.IsNull) continue;
                            var fl = mod.FormLists.FirstOrDefault(f => f.FormKey == c.CreatedObject.FormKey);
                            if (fl == null) continue;
                            if (fl.Items.Any(itm => itm.FormKey == gbfm.FormKey))
                            { setCobjEid = c.EditorID; setCobjKey = c.FormKey; setFlst = fl.EditorID; break; }
                        }
                    chain["setCobj"] = Node(setCobjEid, setCobjKey, ("flst", setFlst));

                    // SNAP NODES -- emitted as FACTS (direction + offset per node); the ordering rule
                    // that grades them lives in Python, same seam as every other leg here.
                    //
                    // Added 2026-08-17 after the Fore/Aft label defect SHIPPED TWICE in seven days
                    // (cargosm_01 08-10, fuel_01 08-17), both caught by his eye on the glass and
                    // neither by any check. The direction table is gen_inspect's, REFERENCED rather
                    // than copied -- a face-name map open-coded twice is two places to get a flip
                    // wrong, and it is the very table the defect turned on.
                    //
                    // ⭐ FOUND VIA THE MSTT'S OWN SnapTemplate LINK, 2026-09-15, not by name. The
                    // distinction that matters for a bay or a hab: `SnapTemplate: Null` is the
                    // CORRECT shell shape for those classes, and a name lookup could only report it
                    // as "not found", which is the same word it uses for a broken chain. A DELIBERATE
                    // absence and a MISSING record must not print as the same thing.
                    var sntp = !mstt.SnapTemplate.IsNull
                        ? mod.SnapTemplates.FirstOrDefault(s => s.FormKey == mstt.SnapTemplate.FormKey)
                        : null;
                    var snapNodes = new List<Dictionary<string, object?>>();
                    if (sntp != null)
                        foreach (var n in sntp.Nodes)
                        {
                            var fid = n.Node.FormKey.ID;
                            snapNodes.Add(new Dictionary<string, object?>
                            {
                                // null = a node this table does not name (equipment/weapon mounts are
                                // the bulk of them). Reported as null rather than "?" so Python can
                                // SKIP it explicitly instead of guessing at a face.
                                ["dir"] = gen_inspect.SnapNodeDirections.TryGetValue(fid, out var dn) ? dn : null,
                                ["nodeId"] = n.NodeID,
                                ["offset"] = new[] { n.Offset.X, n.Offset.Y, n.Offset.Z },
                            });
                        }
                    chain["sntp"] = Node(sntp?.EditorID, sntp?.FormKey,
                        ("nodes", snapNodes),
                        // true = the MSTT deliberately carries no snap template (bay/hab shell
                        // shape). false + found=false = the link is set and the record is missing,
                        // which is a broken chain. Two different states, two different words.
                        ("snapTemplateIsNull", mstt.SnapTemplate.IsNull),
                        ("expectedEditorId", prefix + "_sntp_" + part),
                        ("nameMatchesConvention", Eid(sntp?.EditorID, prefix + "_sntp_" + part)));

                    outp["ok"] = true;
                    return outp;
                }

                // ---- dispatch: one item, or a batch, through the same body ----
                if (item.StartsWith("@", StringComparison.Ordinal))
                {
                    List<string> items;
                    if (string.Equals(item, "@all", StringComparison.OrdinalIgnoreCase))
                    {
                        // Every MoveableStatic carrying the "_ms_" infix, item = the text after it.
                        // ⚠ STATED LIMIT: this enumerates parts that HAVE a MoveableStatic. A part
                        // with a NIF and no MSTT (a DOOR base, by design) is invisible here -- the
                        // same discovery hole named in the manual, not a new one.
                        items = mod.MoveableStatics
                            .Where(m => m.EditorID != null && m.EditorID.Contains("_ms_", StringComparison.OrdinalIgnoreCase))
                            .Select(m => m.EditorID!.Substring(m.EditorID!.IndexOf("_ms_", StringComparison.OrdinalIgnoreCase) + 4))
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                            .ToList();
                    }
                    else if (item.StartsWith("@items:", StringComparison.OrdinalIgnoreCase))
                    {
                        items = item.Substring("@items:".Length)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();
                    }
                    else
                    {
                        return Emit(new Dictionary<string, object?>
                        {
                            ["modname"] = modname,
                            ["part"] = item,
                            ["ok"] = false,
                            ["error"] = $"unknown sentinel '{item}' -- expected @all or @items:a,b,c",
                        });
                    }

                    foreach (var it in items) Emit(InspectPart(it));
                    return 0;
                }

                return Emit(InspectPart(item));
            }
            catch (Exception e)
            {
                return Emit(new Dictionary<string, object?>
                {
                    ["modname"] = modname,
                    ["part"] = item,
                    ["ok"] = false,
                    ["error"] = e.GetType().Name + ": " + e.Message,
                });
            }
        }

        private static bool Eid(string? actual, string expected)
            => actual != null && string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);

        private static Dictionary<string, object?> Node(string? editorId, FormKey? formKey, params (string, object?)[] extra)
        {
            var d = new Dictionary<string, object?>
            {
                ["found"] = editorId != null,
                ["editorId"] = editorId,
                ["formKey"] = formKey?.ToString(),
            };
            foreach (var (k, v) in extra) d[k] = v;
            return d;
        }

        private static int Emit(Dictionary<string, object?> outp)
        {
            Console.WriteLine("CHECKPART_JSON " + JsonSerializer.Serialize(outp));
            return 0;
        }
    }
}
