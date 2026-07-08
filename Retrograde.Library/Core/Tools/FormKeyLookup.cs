using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Retrograde.Abstractions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Retrograde.Utils
{
    public class FormKeyLookup
    {
        // Cache for Starfield.esm + template mods — built once per context, O(1) lookups thereafter.
        // targetMod is NOT cached here because it grows during generation.
        private static IModContext? _cachedContext;
        private static Dictionary<string, FormKey>? _cache;

        // Set by InitializeCache before the first GetFormKey call.
        private static string? _cacheFile;
        private static IReadOnlyList<string>? _esmPaths;

        /// <summary>
        /// Call once after template mod discovery. Provides the file paths needed to
        /// fingerprint the ESMs and locate the cache file on disk.
        /// </summary>
        public static void InitializeCache(string cacheDir, IReadOnlyList<string> esmPaths)
        {
            _cacheFile = Path.Combine(cacheDir, "frankycli_fkl.cache");
            _esmPaths = esmPaths;
        }

        private static Dictionary<string, FormKey> GetCache()
        {
            var ctx = RetrogradeContext.Current;
            if (_cache != null && ReferenceEquals(_cachedContext, ctx))
                return _cache;

            // Try disk cache first.
            if (_cacheFile != null && _esmPaths != null && File.Exists(_cacheFile))
            {
                var loaded = TryLoadFromDisk(_cacheFile, _esmPaths);
                if (loaded != null)
                {
                    _cache = loaded;
                    _cachedContext = ctx;
                    return _cache;
                }
            }

            // Full scan fallback.
            // Uses per-group typed access rather than EnumerateMajorRecords() — the unified
            // iterator terminates on unknown component types introduced by ESM updates, while
            // individual group properties remain accessible.
            Console.WriteLine("[FormKeyLookup] Building EditorID cache from Starfield.esm + template mods...");

            var cache = new Dictionary<string, FormKey>(StringComparer.Ordinal);

            // Starfield.esm first (lower priority — template mods may override)
            IndexModGroups(ctx.StarfieldMod, cache, "Starfield.esm");

            // Template mods overwrite Starfield entries (higher priority, matches original search order)
            foreach (var templateMod in ctx.TemplateMods)
                IndexModGroups(templateMod, cache, templateMod.ModKey.FileName);

            Console.WriteLine($"[FormKeyLookup] Cache ready: {cache.Count} records indexed.");

            if (_cacheFile != null && _esmPaths != null)
                SaveToDisk(_cacheFile, _esmPaths, cache);

            _cache = cache;
            _cachedContext = ctx;
            return cache;
        }

        public static FormKey GetFormKey(string editorID)
        {
            // targetMod grows during generation — must scan linearly to pick up new records
            foreach (var rec in RetrogradeContext.Current.TargetMod.EnumerateMajorRecords())
                if (rec.EditorID == editorID)
                    return rec.FormKey;

            // Starfield.esm + template mods are immutable during a run — use O(1) cache
            if (GetCache().TryGetValue(editorID, out var formKey))
                return formKey;

            throw new KeyNotFoundException($"FormKeyLookup: no record found with EditorID '{editorID}'. Check that the record exists in the target mod, a template mod, or Starfield.esm.");
        }

        // Iterates all IStarfieldGroupGetter<T> / IStarfieldListGroupGetter<T> properties on the mod
        // and indexes each record's EditorID → FormKey into the cache.
        // Each group is enumerated independently so a parse failure in one group does not stop others.
        private static void IndexModGroups(IStarfieldModGetter mod, Dictionary<string, FormKey> cache, string label)
        {
            foreach (var prop in mod.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var typeName = prop.PropertyType.Name;
                if (!typeName.StartsWith("IStarfieldGroupGetter") &&
                    !typeName.StartsWith("IStarfieldListGroupGetter")) continue;

                object? groupVal;
                try { groupVal = prop.GetValue(mod); }
                catch { continue; }
                if (groupVal is not IEnumerable enumerable) continue;

                foreach (var item in EnumerateGroupSafe(enumerable, prop.Name, label))
                {
                    if (item.EditorID != null)
                        cache[item.EditorID] = item.FormKey;
                }
            }
        }

        // Enumerates a group's records, skipping individual records that throw on parse.
        private static IEnumerable<IMajorRecordGetter> EnumerateGroupSafe(
            IEnumerable source, string groupName, string modLabel)
        {
            var en = source.GetEnumerator();
            int consecutiveFaults = 0;
            while (true)
            {
                bool moved;
                try
                {
                    moved = en.MoveNext();
                    consecutiveFaults = 0;
                }
                catch (Exception ex)
                {
                    consecutiveFaults++;
                    Console.WriteLine($"[FormKeyLookup] Skipped record in {modLabel}/{groupName}: {ex.Message}");
                    if (consecutiveFaults >= 20)
                    {
                        Console.WriteLine($"[FormKeyLookup] Aborting {groupName} after 20 consecutive errors.");
                        yield break;
                    }
                    continue;
                }
                if (!moved) yield break;
                if (en.Current is IMajorRecordGetter rec)
                    yield return rec;
            }
        }

        // Enumerates major records from a mod, skipping any that throw (e.g. BGSAdaptiveTriggerData_Component).
        private static IEnumerable<IMajorRecordGetter> EnumerateSafe(IEnumerable<IMajorRecordGetter> source, string label)
        {
            using var en = source.GetEnumerator();
            while (true)
            {
                bool moved;
                try { moved = en.MoveNext(); }
                catch (Exception ex)
                {
                    Console.WriteLine($"[FormKeyLookup] Skipped broken record in {label}: {ex.Message}");
                    continue;
                }
                if (!moved) yield break;
                yield return en.Current;
            }
        }

        // ── Disk cache ──────────────────────────────────────────────────────────

        private static bool FingerprintMatches(JsonElement root, IReadOnlyList<string> esmPaths)
        {
            if (!root.TryGetProperty("fingerprint", out var fp))
                return false;

            int i = 0;
            foreach (var entry in fp.EnumerateArray())
            {
                if (i >= esmPaths.Count) return false;
                var path = entry.GetProperty("p").GetString();
                if (path != esmPaths[i]) return false;

                var info = new FileInfo(esmPaths[i]);
                if (!info.Exists) return false;
                if (info.Length != entry.GetProperty("s").GetInt64()) return false;
                if (info.LastWriteTimeUtc.Ticks != entry.GetProperty("t").GetInt64()) return false;
                i++;
            }

            return i == esmPaths.Count;
        }

        private static Dictionary<string, FormKey>? TryLoadFromDisk(string cacheFile, IReadOnlyList<string> esmPaths)
        {
            try
            {
                var bytes = File.ReadAllBytes(cacheFile);
                using var doc = JsonDocument.Parse(bytes);
                var root = doc.RootElement;

                if (!root.TryGetProperty("v", out var ver) || ver.GetInt32() != 1)
                    return null;

                if (!FingerprintMatches(root, esmPaths))
                {
                    Console.WriteLine("[FormKeyLookup] Cache miss or stale — rebuilding from ESMs...");
                    return null;
                }

                var entries = root.GetProperty("e");
                var cache = new Dictionary<string, FormKey>(StringComparer.Ordinal);

                foreach (var entry in entries.EnumerateArray())
                {
                    // entry = [editorId, modKeyName, modKeyType(int), formId(uint)]
                    var editorId = entry[0].GetString()!;
                    var modKeyName = entry[1].GetString()!;
                    var modKeyType = (ModType)entry[2].GetInt32();
                    var formId = entry[3].GetUInt32();

                    var modKey = new ModKey(modKeyName, modKeyType);
                    cache[editorId] = new FormKey(modKey, formId);
                }

                Console.WriteLine($"[FormKeyLookup] Cache loaded from disk ({cache.Count} records).");
                return cache;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FormKeyLookup] Cache read failed ({ex.GetType().Name}), rebuilding...");
                return null;
            }
        }

        private static void SaveToDisk(string cacheFile, IReadOnlyList<string> esmPaths, Dictionary<string, FormKey> cache)
        {
            try
            {
                using var stream = new FileStream(cacheFile, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new Utf8JsonWriter(stream);

                writer.WriteStartObject();
                writer.WriteNumber("v", 1);

                // Fingerprint
                writer.WriteStartArray("fingerprint");
                foreach (var path in esmPaths)
                {
                    var info = new FileInfo(path);
                    writer.WriteStartObject();
                    writer.WriteString("p", path);
                    writer.WriteNumber("s", info.Length);
                    writer.WriteNumber("t", info.LastWriteTimeUtc.Ticks);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();

                // Entries
                writer.WriteStartArray("e");
                foreach (var (editorId, fk) in cache)
                {
                    writer.WriteStartArray();
                    writer.WriteStringValue(editorId);
                    writer.WriteStringValue(fk.ModKey.Name);
                    writer.WriteNumberValue((int)fk.ModKey.Type);
                    writer.WriteNumberValue(fk.ID);
                    writer.WriteEndArray();
                }
                writer.WriteEndArray();

                writer.WriteEndObject();
                Console.WriteLine($"[FormKeyLookup] Cache saved ({cache.Count} records).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[FormKeyLookup] Cache save failed ({ex.GetType().Name}): {ex.Message}");
            }
        }
    }
}
