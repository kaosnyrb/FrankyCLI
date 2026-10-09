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

    /// The expanded-refs cache: one file per PackIn FormKey under %TEMP%\FrankyCLI\packin-cache\<load order>\,
    /// read before the cell is loaded (~19s per PackIn, measured 2026-10-09) and written after. His ask.
    /// The subfolder is a FINGERPRINT of the load order (every plugin's name, size and modified time,
    /// plus CacheVersion), so editing any plugin starts a fresh folder instead of serving stale refs.
    /// Bump CacheVersion when the shape CellRefs writes changes. A PackIn with any unresolved base is
    /// never written, so a failure is not frozen into the cache. Delete the folder to clear it.
    const int CacheVersion = 1;
    static string? _cacheDir;

    static string CacheDir(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
    {
        if (_cacheDir != null) return _cacheDir;
        var sb = new System.Text.StringBuilder($"v{CacheVersion}\n");
        foreach (var listing in env.LoadOrder.ListedOrder)
        {
            var f = new FileInfo(Path.Combine(env.DataFolderPath, listing.ModKey.FileName));
            sb.Append(listing.ModKey.FileName).Append('|')
              .Append(f.Exists ? $"{f.Length}|{f.LastWriteTimeUtc.Ticks}" : "absent").Append('\n');
        }
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sb.ToString())))[..16];
        _cacheDir = Path.Combine(Path.GetTempPath(), "FrankyCLI", "packin-cache", hash);
        Directory.CreateDirectory(_cacheDir);
        return _cacheDir;
    }

    static string CachePath(FormKey fk) =>
        Path.Combine(_cacheDir!, $"{fk.ID:X6}_{fk.ModKey.FileName}.json");

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
        Console.WriteLine($"  cache: {CacheDir(env)}");
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
            doc["refs"] = CellRefs(pk, cache, 0, ref unresolved);
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
    static IMajorRecordGetter? ResolveBase(FormKey fk, ILinkCache cache)
    {
        if (cache.TryResolve<IStaticGetter>(fk, out var st)) return st;
        if (cache.TryResolve<IPackInGetter>(fk, out var pk)) return pk;
        if (cache.TryResolve<IMoveableStaticGetter>(fk, out var ms)) return ms;
        if (cache.TryResolve<ILightGetter>(fk, out var li)) return li;
        if (cache.TryResolve<IFurnitureGetter>(fk, out var fu)) return fu;
        if (cache.TryResolve<IActivatorGetter>(fk, out var ac)) return ac;
        if (cache.TryResolve<IContainerGetter>(fk, out var co)) return co;
        if (cache.TryResolve<IDoorGetter>(fk, out var dr)) return dr;
        var t = DateTime.Now;
        var any = cache.TryResolve<IStarfieldMajorRecordGetter>(fk, out var rec) ? rec : null;
        Console.WriteLine($"  SLOW untyped resolve of {fk} ({(DateTime.Now - t).TotalSeconds:F1}s): {any?.GetType().Name.Replace("BinaryOverlay", "") ?? "not found"}; add its kind to ResolveBase");
        return any;
    }

    /// A PackIn's refs, nested PackIns expanded: from the cache when it has them, else from the cell.
    static JsonArray CellRefs(IPackInGetter pk, ILinkCache cache, int depth, ref int unresolved)
    {
        var path = CachePath(pk.FormKey);
        if (File.Exists(path))
        {
            var hit = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
            Console.WriteLine($"  cache hit {pk.EditorID} ({pk.FormKey}): {Count(hit)} refs");
            return hit;
        }
        int before = unresolved;
        var arr = LoadCellRefs(pk, cache, depth, ref unresolved);
        if (unresolved == before)
            File.WriteAllText(path, arr.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return arr;
    }

    static JsonArray LoadCellRefs(IPackInGetter pk, ILinkCache cache, int depth, ref int unresolved)
    {
        var arr = new JsonArray();
        var tc = DateTime.Now;
        if (!pk.Cell.TryResolve(cache, out var cell))
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
            if (po.Base.IsNull || ResolveBase(po.Base.FormKey, cache) is not { } baseRec)
            {
                Console.WriteLine($"  UNRESOLVED base {po.Base.FormKey} of {po.FormKey} in {pk.EditorID}");
                unresolved++;
            }
            else
            {
                if (!string.IsNullOrEmpty(baseRec.EditorID)) r["eid"] = baseRec.EditorID;
                Describe(r, baseRec, cache, depth, ref unresolved);
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
        if (ResolveBase(fk, cache) is not { } rec)
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
        Describe(r, rec, cache, 0, ref unresolved);
    }

    /// What a resolved base adds to its ref: `kind`, and either `nif` or, for a nested PackIn, its `refs`.
    static void Describe(JsonObject r, IMajorRecordGetter rec, ILinkCache cache, int depth, ref int unresolved)
    {
        string key = r["base"]!.GetValue<string>();
        r["kind"] = rec.GetType().Name.Replace("BinaryOverlay", "");
        if (rec is IPackInGetter nested)
        {
            if (depth >= MaxDepth) throw new Exception($"PackIns nested past {MaxDepth} at {key}");
            r["refs"] = CellRefs(nested, cache, depth + 1, ref unresolved);
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
