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
    /// </summary>
    public class gen_checkpart
    {
        public static int Generate(string[] args)
        {
            string modname = args[0];
            string item = args[2];

            var outp = new Dictionary<string, object?> { ["modname"] = modname, ["part"] = item };
            try
            {
                using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
                var cache = env.LinkCache;

                IStarfieldModGetter? mod = null;
                foreach (var listing in env.LoadOrder.ListedOrder)
                    if (listing.Mod != null && string.Equals(listing.ModKey.Name, modname, StringComparison.OrdinalIgnoreCase))
                    { mod = listing.Mod; break; }

                if (mod == null)
                {
                    outp["ok"] = false;
                    outp["error"] = $"{modname}.esm is not in the active load order -- enable it, or it can't be audited";
                    return Emit(outp);
                }

                // master type -- authored as ModType.Master; report what the header actually says
                var hflags = mod.ModHeader.Flags;
                string ft = hflags.ToString();
                bool isMaster = ft.Contains("Master", StringComparison.OrdinalIgnoreCase);
                bool isSmall = ft.Contains("Small", StringComparison.OrdinalIgnoreCase) || ft.Contains("Light", StringComparison.OrdinalIgnoreCase);
                outp["masterType"] = isMaster ? (isSmall ? "Small" : "Full") : "None";
                outp["masterFlags"] = ft;

                // MoveableStatic -- found by the "_ms_<item>" suffix so <prefix> is read, not passed
                string msttSuffix = "_ms_" + item;
                var mstt = mod.MoveableStatics.FirstOrDefault(
                    m => m.EditorID != null && m.EditorID.EndsWith(msttSuffix, StringComparison.OrdinalIgnoreCase));
                var msttOut = new Dictionary<string, object?>();
                outp["mstt"] = msttOut;
                if (mstt == null)
                {
                    msttOut["found"] = false;
                    outp["ok"] = true;                 // ran fine; the part just isn't there
                    return Emit(outp);
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

                // chain: PKIN -> GBFM -> COBJ, all by <prefix>_<tag>_<item>
                var chain = new Dictionary<string, object?>();
                outp["chain"] = chain;

                var pkin = mod.PackIns.FirstOrDefault(p => Eid(p.EditorID, prefix + "_pkn_" + item));
                string? cellRef = null;
                if (pkin != null && !pkin.Cell.IsNull) cache.TryResolveIdentifier(pkin.Cell.FormKey, out cellRef);
                chain["pkin"] = Node(pkin?.EditorID, pkin?.FormKey, ("cellRef", cellRef));

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
                    ? mod.Cells.Records.SelectMany(b => b.SubBlocks).SelectMany(sb => sb.Cells)
                          .FirstOrDefault(c => c.FormKey == pkin.Cell.FormKey)
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

                var gbfm = mod.GenericBaseForms.FirstOrDefault(g => Eid(g.EditorID, prefix + "_gbfm_" + item));
                string? packInRef = null;
                if (gbfm != null)
                    foreach (var comp in gbfm.Components)
                        if (comp is IFormLinkDataComponentGetter fld)
                            foreach (var l in fld.Links)
                                if (!l.LinkedForm.IsNull && cache.TryResolveIdentifier(l.LinkedForm.FormKey, out var r)) packInRef = r;
                chain["gbfm"] = Node(gbfm?.EditorID, gbfm?.FormKey, ("packInRef", packInRef));

                var cobj = mod.ConstructibleObjects.FirstOrDefault(c => Eid(c.EditorID, prefix + "_co_" + item));
                string? createdRef = null;
                if (cobj != null && !cobj.CreatedObject.IsNull) cache.TryResolveIdentifier(cobj.CreatedObject.FormKey, out createdRef);
                chain["cobj"] = Node(cobj?.EditorID, cobj?.FormKey, ("createdObjectRef", createdRef));

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
                var sntp = mod.SnapTemplates.FirstOrDefault(s => Eid(s.EditorID, prefix + "_sntp_" + item));
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
                chain["sntp"] = Node(sntp?.EditorID, sntp?.FormKey, ("nodes", snapNodes));

                outp["ok"] = true;
                return Emit(outp);
            }
            catch (Exception e)
            {
                outp["ok"] = false;
                outp["error"] = e.GetType().Name + ": " + e.Message;
                return Emit(outp);
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
