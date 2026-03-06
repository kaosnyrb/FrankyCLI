using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Retrograde.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde.Utils
{
    /// <summary>
    /// Generic three-tier record search: TargetMod → TemplateMods → (optionally) Starfield.esm.
    /// Replaces the duplicated inline search loops found across Noun classes.
    /// </summary>
    public static class RecordLookup
    {
        /// <summary>
        /// Finds a record by raw FormID across TargetMod and TemplateMods (and optionally Starfield.esm).
        /// </summary>
        public static T Find<T>(
            uint formId,
            Func<IStarfieldModGetter, IEnumerable<T>> group,
            bool searchStarfield = false)
            where T : class, IMajorRecordGetter
        {
            var ctx = RetrogradeContext.Current;

            T? hit = group(ctx.TargetMod)
                .FirstOrDefault(r => r.FormKey == new FormKey(ctx.TargetMod.ModKey, formId));

            if (hit == null)
                foreach (var tm in ctx.TemplateMods)
                {
                    hit = group(tm).FirstOrDefault(r => r.FormKey == new FormKey(tm.ModKey, formId));
                    if (hit != null) break;
                }

            if (hit == null && searchStarfield)
                hit = group(ctx.StarfieldMod)
                    .FirstOrDefault(r => r.FormKey == new FormKey(ctx.StarfieldModKey, formId));

            return hit ?? throw new KeyNotFoundException(
                $"RecordLookup: no {typeof(T).Name} with raw ID 0x{formId:X6} found in TargetMod or TemplateMods.");
        }

        /// <summary>
        /// Finds a record by EditorID across TargetMod and TemplateMods (and optionally Starfield.esm).
        /// </summary>
        public static T Find<T>(
            string editorId,
            Func<IStarfieldModGetter, IEnumerable<T>> group,
            bool searchStarfield = false)
            where T : class, IMajorRecordGetter
        {
            var ctx = RetrogradeContext.Current;

            T? hit = group(ctx.TargetMod).FirstOrDefault(r => r.EditorID == editorId);

            if (hit == null)
                foreach (var tm in ctx.TemplateMods)
                {
                    hit = group(tm).FirstOrDefault(r => r.EditorID == editorId);
                    if (hit != null) break;
                }

            if (hit == null && searchStarfield)
                hit = group(ctx.StarfieldMod).FirstOrDefault(r => r.EditorID == editorId);

            return hit ?? throw new KeyNotFoundException(
                $"RecordLookup: no {typeof(T).Name} with EditorID '{editorId}' found in TargetMod or TemplateMods.");
        }
    }
}
