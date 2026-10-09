using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Noggog;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FrankyCLI;

/// <summary>
/// A PackIn as a JSON file, so a room can be DESIGNED as data (Kim's lane) and LOOKED AT in clay
/// (scripts/nif/clay.py) before it is built. His ask, 2026-10-08: "let Kim design packin json files
/// which we can convert into real packins". Spike: home-office office/spikes/2026-10-08-render-whole-packins.md.
///
///   packin export &lt;PackIn FormKey&gt; &lt;out.json&gt;                an existing PackIn, nested PackIns expanded
///   packin resolve &lt;in.json&gt; &lt;out.json&gt;                      a designed file, every base resolved
///
/// The file names BASE FORMS, not meshes, because a PackIn places forms and the kit pieces are often
/// PackIns themselves:
///
///   {"name": "kim_room_01", "refs": [
///     {"base": "02447F:Starfield.esm", "eid": "SciIntHallSm1Way01__SC", "pos": [0, 4, 0], "rot": [0, 0, 1.5708]}]}
///
/// `pos` in game units, `rot` in RADIANS exactly as the REFR stores it (no conversion anywhere), `scale`
/// optional (1.0). `base` is ALWAYS a FormKey (FormID + ModKey, `02447F:Starfield.esm`), in both
/// directions; a name there is refused. His call, 2026-10-09: a FormKey resolves directly, a name needs
/// a search over every record in the load order, and the untyped name search hung the rig overnight on
/// 2026-10-08. `eid` is an optional human label: resolve CHECKS it against the record the FormKey found,
/// so a mistyped FormID that lands on a different piece fails loud instead of placing the wrong mesh.
/// Resolve and export ADD, per ref: `eid` (when absent), `kind` (the base record type), `nif` (its model, `meshes/...`, or
/// absent when the base has none: markers, lights) and, for a nested PackIn, its own `refs`. Export also
/// writes the PackIn's own `bounds` (OBND) and `persistent: true` on refs from the cell's persistent list.
/// The renderer composes the transforms; the rotation axis ORDER is measured there, not assumed here.
/// Unknown keys are REFUSED: a misspelled `rotation` must not become a silent zero.
/// </summary>
public static class packin_json
{
    static readonly HashSet<string> RefKeys = new() { "base", "eid", "pos", "rot", "scale", "nif", "kind", "refs", "persistent", "note" };
    static readonly HashSet<string> TopKeys = new() { "name", "refs", "bounds", "source", "note" };
    const int MaxDepth = 8;

    /// The expanded-refs cache: one file per PackIn FormKey under %TEMP%\FrankyCLI\packin-cache\, read
    /// before the cell is loaded (~10-19s per PackIn, measured 2026-10-09) and written after. His ask, and
    /// his refinement: "Starfield.esm like never changes", so an entry must not die when an unrelated
    /// plugin is edited.
    ///
    /// So an entry is keyed on WHAT IT READ, not on the whole load order. It stores:
    ///   touched  every FormKey it read (the PackIn, its cell, each placed ref, each base, nested ones too)
    ///   deps     the plugin each WINNING version came from
    ///   plugins  every plugin's position, size and modified time when it was written
    /// and a hit needs: every dep unchanged, and no OTHER changed or added plugin containing a touched
    /// FormKey (that plugin is scanned once per run; Starfield.esm never changes, so it is never scanned).
    /// Overriding a ref means carrying its cell, and the cell is touched, so a new ref override is caught
    /// through the cell. Bump CacheVersion when the entry shape changes. A PackIn with any unresolved base
    /// is never written. Delete the folder to clear it.
    const int CacheVersion = 2;
    static readonly string CacheDir = Path.Combine(Path.GetTempPath(), "FrankyCLI", "packin-cache");
    static IGameEnvironment<IStarfieldMod, IStarfieldModGetter> _env = null!;
    static Dictionary<string, string> _plugins = null!;                 // name -> "position|size|ticks"
    static readonly Dictionary<string, HashSet<string>> _scanned = new(); // a changed plugin's FormKeys, once per run

    sealed class Trace
    {
        public readonly HashSet<string> Touched = new();
        public readonly HashSet<string> Deps = new();
        public void Read(FormKey fk, ModKey winner) { Touched.Add(fk.ToString()); Deps.Add(winner.FileName); }
        public void Merge(Trace t) { Touched.UnionWith(t.Touched); Deps.UnionWith(t.Deps); }
    }

    static Dictionary<string, string> PluginStamps()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        foreach (var listing in _env.LoadOrder.ListedOrder)
        {
            var f = new FileInfo(Path.Combine(_env.DataFolderPath, listing.ModKey.FileName));
            d[listing.ModKey.FileName] = f.Exists ? $"{i}|{f.Length}|{f.LastWriteTimeUtc.Ticks}" : $"{i}|absent";
            i++;
        }
        return d;
    }

    static string CachePath(FormKey fk) => Path.Combine(CacheDir, $"{fk.ID:X6}_{fk.ModKey.FileName}.json");

    /// Why an entry cannot be used, or null when it can.
    static string? Stale(JsonObject e, Trace t)
    {
        if (e["v"]?.GetValue<int>() != CacheVersion) return "older cache version";
        var then = e["plugins"]!.AsObject();
        foreach (var dep in t.Deps)
            if (!_plugins.TryGetValue(dep, out var now) || then[dep]?.GetValue<string>() != now)
                return $"{dep} changed";
        foreach (var (name, now) in _plugins)
        {
            if (then[name]?.GetValue<string>() == now || t.Deps.Contains(name)) continue;
            if (!_scanned.TryGetValue(name, out var keys))
            {
                var ts = DateTime.Now;
                var mod = _env.LoadOrder.ListedOrder.First(l => l.ModKey.FileName == name).Mod;
                keys = mod == null ? new() : mod.EnumerateMajorRecords().Select(r => r.FormKey.ToString()).ToHashSet();
                _scanned[name] = keys;
                Console.WriteLine($"  [{(DateTime.Now - ts).TotalSeconds:F1}s] scanned changed plugin {name}: {keys.Count} records");
            }
            if (t.Touched.Overlaps(keys)) return $"{name} changed and overrides a record this PackIn read";
        }
        return null;
    }

    public static int Run(string[] args)
    {
        if (args.Length < 4 || (args[1] != "export" && args[1] != "resolve"))
        {
            Console.WriteLine("Usage: packin export <PackIn FormKey, e.g. 02447F:Starfield.esm> <out.json>");
            Console.WriteLine("       packin resolve <in.json> <out.json>");
            return 1;
        }
        var t0 = DateTime.Now;
        using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
        Console.WriteLine($"  [{(DateTime.Now - t0).TotalSeconds:F1}s] environment: {env.LoadOrder.Count} plugins");
        var cache = env.LinkCache;
        _env = env;
        _plugins = PluginStamps();
        Directory.CreateDirectory(CacheDir);
        Console.WriteLine($"  cache: {CacheDir}");
        int unresolved = 0;
        JsonObject doc;
        if (args[1] == "export")
        {
            var pkKey = ParseKey(args[2], "packin export");
            var pk = cache.TryResolve<IPackInGetter>(pkKey, out var found) ? found
                : throw new Exception($"no PackIn {pkKey} in the load order");
            Console.WriteLine($"  [{(DateTime.Now - t0).TotalSeconds:F1}s] found {pk.EditorID}");
            doc = new JsonObject
            {
                ["name"] = pk.EditorID ?? pk.FormKey.ToString(),
                ["source"] = pk.FormKey.ToString(),
            };
            if (pk.ObjectBounds is { } ob)
                doc["bounds"] = new JsonArray(Vec(ob.First), Vec(ob.Second));
            doc["refs"] = CellRefs(pk, cache, 0, new Trace(), ref unresolved);
        }
        else
        {
            doc = JsonNode.Parse(File.ReadAllText(args[2]))!.AsObject();
            CheckKeys(doc, TopKeys, "the top level");
            var refs = doc["refs"]?.AsArray() ?? throw new Exception("no `refs` array");
            foreach (var r in refs)
                ResolveRef(r!.AsObject(), cache, ref unresolved);
        }
        File.WriteAllText(args[3], doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        int total = Count(doc["refs"]!.AsArray());
        Console.WriteLine($"  {args[3]}: {total} refs (nested included), {unresolved} unresolved");
        return unresolved > 0 ? 2 : 0;
    }

    /// A FormKey or a loud refusal. There is no name path to fall back to, on purpose.
    static FormKey ParseKey(string key, string where) =>
        key.Contains(':') && FormKey.TryFactory(key, out var fk) ? fk
            : throw new Exception($"{where}: '{key}' is not a FormKey; write FormID:ModKey, e.g. 02447F:Starfield.esm (names are not looked up)");

    static JsonArray Vec(P3Float p) => new JsonArray(p.X, p.Y, p.Z);

    /// A base record by FormKey, trying the TYPED caches first. Measured 2026-10-09 on
    /// SciIntHallSm1Way01__SC: the untyped resolve cost ~23s and gigabytes PER REF (7 refs: 163s, 24 GB
    /// peak), where a typed one only builds its own group. The untyped resolve stays as the last resort
    /// and says so out loud, so a kind missing from this list shows up as a named slow line, not a hang.
    static IMajorRecordGetter? ResolveBase(FormKey fk, ILinkCache cache, Trace trace)
    {
        var rec = Typed<IStaticGetter>(fk, cache, trace) ?? Typed<IPackInGetter>(fk, cache, trace)
            ?? Typed<IMoveableStaticGetter>(fk, cache, trace) ?? Typed<ILightGetter>(fk, cache, trace)
            ?? Typed<IFurnitureGetter>(fk, cache, trace) ?? Typed<IActivatorGetter>(fk, cache, trace)
            ?? Typed<IContainerGetter>(fk, cache, trace) ?? Typed<IDoorGetter>(fk, cache, trace);
        if (rec != null) return rec;
        var t = DateTime.Now;
        var any = Typed<IStarfieldMajorRecordGetter>(fk, cache, trace);
        Console.WriteLine($"  SLOW untyped resolve of {fk} ({(DateTime.Now - t).TotalSeconds:F1}s): {any?.GetType().Name.Replace("BinaryOverlay", "") ?? "not found"}; add its kind to ResolveBase");
        return any;
    }

    /// The winning record of one type, noting which plugin it came from.
    static IMajorRecordGetter? Typed<T>(FormKey fk, ILinkCache cache, Trace trace) where T : class, IMajorRecordGetter
    {
        if (!cache.TryResolveSimpleContext<T>(fk, out var ctx)) return null;
        trace.Read(fk, ctx.ModKey);
        return ctx.Record;
    }

    /// A PackIn's refs, nested PackIns expanded: from the cache when it has them, else from the cell.
    /// A PackIn's refs, nested PackIns expanded: from the cache when the entry still holds, else from
    /// the cell. Either way the parent's trace gains everything this PackIn read.
    static JsonArray CellRefs(IPackInGetter pk, ILinkCache cache, int depth, Trace parent, ref int unresolved)
    {
        var path = CachePath(pk.FormKey);
        if (File.Exists(path))
        {
            var e = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
            var held = new Trace();
            foreach (var fk in e["touched"]?.AsArray() ?? new JsonArray()) held.Touched.Add(fk!.GetValue<string>());
            foreach (var d in e["deps"]?.AsArray() ?? new JsonArray()) held.Deps.Add(d!.GetValue<string>());
            if (Stale(e, held) is { } why)
                Console.WriteLine($"  cache stale {pk.EditorID} ({pk.FormKey}): {why}");
            else
            {
                var hit = e["refs"]!.AsArray();
                Console.WriteLine($"  cache hit {pk.EditorID} ({pk.FormKey}): {Count(hit)} refs");
                parent.Merge(held);
                e.Remove("refs");
                return hit;
            }
        }
        int before = unresolved;
        var trace = new Trace();
        var arr = LoadCellRefs(pk, cache, depth, trace, ref unresolved);
        if (unresolved == before)
        {
            var plugins = new JsonObject();
            foreach (var (name, stamp) in _plugins) plugins[name] = stamp;
            var entry = new JsonObject
            {
                ["v"] = CacheVersion,
                ["packin"] = pk.FormKey.ToString(),
                ["deps"] = new JsonArray(trace.Deps.OrderBy(x => x).Select(x => (JsonNode)x!).ToArray()),
                ["touched"] = new JsonArray(trace.Touched.OrderBy(x => x).Select(x => (JsonNode)x!).ToArray()),
                ["plugins"] = plugins,
                ["refs"] = JsonNode.Parse(arr.ToJsonString()),
            };
            File.WriteAllText(path, entry.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        parent.Merge(trace);
        return arr;
    }

    static JsonArray LoadCellRefs(IPackInGetter pk, ILinkCache cache, int depth, Trace trace, ref int unresolved)
    {
        var arr = new JsonArray();
        var tc = DateTime.Now;
        if (Typed<IPackInGetter>(pk.FormKey, cache, trace) == null || Typed<ICellGetter>(pk.Cell.FormKey, cache, trace) is not ICellGetter cell)
        {
            Console.WriteLine($"  UNRESOLVED cell of PackIn {pk.EditorID} ({pk.Cell.FormKey})");
            unresolved++;
            return arr;
        }
        Console.WriteLine($"  [{(DateTime.Now - tc).TotalSeconds:F1}s] cell {cell.FormKey}: {cell.Persistent.Count + cell.Temporary.Count} entries");
        foreach (var (entry, persistent) in cell.Persistent.Select(e => (e, true)).Concat(cell.Temporary.Select(e => (e, false))))
        {
            if (entry is not IPlacedObjectGetter po)
            {
                // NPCs and other placed kinds are listed, never dropped silently, and never rebuilt
                Console.WriteLine($"  SKIPPED {entry.GetType().Name} {entry.FormKey} in {pk.EditorID}: only placed objects are carried");
                continue;
            }
            // `base` is the FormKey, exact and unique; the EditorID rides along as a label only
            var r = new JsonObject
            {
                ["base"] = po.Base.FormKey.ToString(),
                ["pos"] = Vec(po.Position),
                ["rot"] = Vec(po.Rotation),
            };
            if (po.Scale is float s && s != 1f) r["scale"] = s;
            if (persistent) r["persistent"] = true;
            // not po.Base.TryResolve: that is typed to IPlaceableObject and missed all 7 bases of
            // SciIntHallSm1Way01__SC on 2026-10-09
            // the ref itself: its winner is the cell's (an override of a ref carries the cell), so it is
            // touched for the changed-plugin scan rather than resolved on its own
            trace.Touched.Add(po.FormKey.ToString());
            if (po.Base.IsNull || ResolveBase(po.Base.FormKey, cache, trace) is not { } baseRec)
            {
                Console.WriteLine($"  UNRESOLVED base {po.Base.FormKey} of {po.FormKey} in {pk.EditorID}");
                unresolved++;
            }
            else
            {
                if (!string.IsNullOrEmpty(baseRec.EditorID)) r["eid"] = baseRec.EditorID;
                Describe(r, baseRec, cache, depth, trace, ref unresolved);
            }
            arr.Add(r);
        }
        Console.WriteLine($"  [{(DateTime.Now - tc).TotalSeconds:F1}s] {pk.EditorID}: {arr.Count} refs described");
        return arr;
    }

    /// A DESIGNED ref (resolve mode): `base` must be a FormKey and resolves directly. A written `eid` is
    /// checked against what that FormKey found; a mismatch counts as unresolved (exit 2), never a warning.
    static void ResolveRef(JsonObject r, ILinkCache cache, ref int unresolved)
    {
        CheckKeys(r, RefKeys, $"ref {r["base"]}");
        string key = r["base"]?.GetValue<string>() ?? throw new Exception("a ref has no `base`");
        foreach (var k in new[] { "pos", "rot" })
            if (r[k] is not JsonArray a || a.Count != 3)
                throw new Exception($"ref {key}: `{k}` must be [x, y, z]");
        var fk = ParseKey(key, "ref base");
        var trace = new Trace();   // resolve mode writes no entry of its own; nested PackIns still cache
        if (ResolveBase(fk, cache, trace) is not { } rec)
        {
            Console.WriteLine($"  UNRESOLVED base {fk}");
            unresolved++;
            return;
        }
        string? written = r["eid"]?.GetValue<string>();
        if (written != null && !string.Equals(written, rec.EditorID, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  MISMATCH base {fk}: file says eid '{written}', the record is '{rec.EditorID ?? "(no EditorID)"}'");
            unresolved++;
            return;
        }
        if (written == null && !string.IsNullOrEmpty(rec.EditorID)) r["eid"] = rec.EditorID;
        Describe(r, rec, cache, 0, trace, ref unresolved);
    }

    /// What a resolved base adds to its ref: `kind`, and either `nif` or, for a nested PackIn, its `refs`.
    static void Describe(JsonObject r, IMajorRecordGetter rec, ILinkCache cache, int depth, Trace trace, ref int unresolved)
    {
        string key = r["base"]!.GetValue<string>();
        r["kind"] = rec.GetType().Name.Replace("BinaryOverlay", "");
        if (rec is IPackInGetter nested)
        {
            if (depth >= MaxDepth) throw new Exception($"PackIns nested past {MaxDepth} at {key}");
            r["refs"] = CellRefs(nested, cache, depth + 1, trace, ref unresolved);
        }
        else if (rec is IModeledGetter mg && mg.Model?.File != null)
        {
            string p = mg.Model.File.DataRelativePath.Path.Replace('\\', '/').ToLowerInvariant();
            r["nif"] = p.StartsWith("meshes/") ? p : "meshes/" + p;
        }
    }

    static void CheckKeys(JsonObject o, HashSet<string> allowed, string where)
    {
        foreach (var kv in o)
            if (!allowed.Contains(kv.Key))
                throw new Exception($"unknown key `{kv.Key}` in {where}; allowed: {string.Join(", ", allowed)}");
    }

    static int Count(JsonArray refs) =>
        refs.Sum(r => 1 + (r?["refs"] is JsonArray c ? Count(c) : 0));
}
