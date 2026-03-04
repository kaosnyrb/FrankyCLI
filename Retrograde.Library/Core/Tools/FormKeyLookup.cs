using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Abstractions;
using System;
using System.Collections.Generic;

namespace Retrograde.Utils
{
    public class FormKeyLookup
    {
        // Cache for Starfield.esm + template mods — built once per context, O(1) lookups thereafter.
        // targetMod is NOT cached here because it grows during generation.
        private static IModContext? _cachedContext;
        private static Dictionary<string, FormKey>? _cache;

        private static Dictionary<string, FormKey> GetCache()
        {
            var ctx = RetrogradeContext.Current;
            if (_cache != null && ReferenceEquals(_cachedContext, ctx))
                return _cache;

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
    }
}
