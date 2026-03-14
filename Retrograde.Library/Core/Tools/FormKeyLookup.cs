using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
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
            Console.WriteLine("[FormKeyLookup] Building EditorID cache from Starfield.esm + template mods...");

            var cache = new Dictionary<string, FormKey>(StringComparer.Ordinal);

            // Starfield.esm first (lower priority — template mods may override)
            foreach (var rec in ctx.StarfieldMod.EnumerateMajorRecords())
                if (rec.EditorID != null)
                    cache[rec.EditorID] = rec.FormKey;

            // Template mods overwrite Starfield entries (higher priority, matches original search order)
            foreach (var templateMod in ctx.TemplateMods)
                foreach (var rec in templateMod.EnumerateMajorRecords())
                    if (rec.EditorID != null)
                        cache[rec.EditorID] = rec.FormKey;

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
