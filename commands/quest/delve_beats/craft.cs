using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// craft: the player MAKES the beat's item at a bench (his 2026-10-26, "we need a Manufacturing mission. These are
        /// about bringing raw material or crafted parts to people. It's crafting quests"; missionboard34a). The build clones
        /// the item as a pickup's (Items pass) and a COBJ for it off his MB11a's (CraftCobjSource), conditioned to the
        /// step: live once the step has begun and until the item is made. The step is done when the item reaches the
        /// player's pack: recover's hook, the stock OnItemAdded on the player alias, filtered to the item.
        /// ⚠ UNPROVEN when written: that OnItemAdded fires on a CRAFT (it does on a pickup). His MB11 to MB13 never asked:
        /// their step is the item put into a container at the site. A craft has no place, so HasPlace is false.
        /// </summary>
        private sealed class CraftBeat : BeatKind
        {
            internal override string Name => "craft";
            internal override bool CarriesItem => true;
            internal override bool HasPlace => false;

            internal override void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn)
            {
                if (!string.IsNullOrEmpty(b.at) || b.place != null)
                    Fatal($"beat {i + 1} is a craft: it is made wherever he has a bench, so at and place would be ignored.");
                if (b.model != null || b.name != null || b.replace || b.outside != null)
                    Fatal($"beat {i + 1} is a craft: it has no object in the world, so model, name, replace and outside would be ignored.");
                if (b.craft == null) { Fatal($"beat {i + 1} is a craft and has no craft {{ bench, ingredients }}."); return; }
                if (b.craft.bench == null || !CraftBenches.ContainsKey(b.craft.bench))
                    Fatal($"beat {i + 1} craft.bench is '{b.craft.bench}'; built: {string.Join(", ", CraftBenches.Keys.Select(k => "\"" + k + "\""))}.");
                if (b.craft.ingredients == null || b.craft.ingredients.Count == 0)
                { Fatal($"beat {i + 1} craft.ingredients is empty, so the item would cost nothing."); return; }
                foreach (var (key, count, err) in ReadIngredients(b.craft))
                {
                    if (err != null) { Fatal($"beat {i + 1} craft: {err}"); continue; }
                    var hit = ResolveIngredient(env, Allowed(env, t), key!, out string why);
                    if (hit == null) Fatal($"beat {i + 1} craft: ingredient '{key}' {why}.");
                    else Console.WriteLine($"  beat {i + 1} craft: {count} x {hit.Name?.String ?? hit.EditorID} [{hit.EditorID} {hit.FormKey}]");
                }
            }

            /// <summary>No aliases: nothing is placed and the objective points at nothing (made wherever he is).</summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                build.slotOf[i] = new BeatSlot { markerAlias = -1, activatorAlias = -1, targetAlias = -1, journalStage = build.beatStage[i] };
                Console.WriteLine($"  +slot    : beat {i + 1} (craft) -- no aliases: made at a bench, wherever he is");
                return 0;
            }

            /// <summary>Recover's hook on the PLAYER, sharing its A-D duplicate count (one alias, one script each).</summary>
            internal override int Hook(BeatsBuild build, int b, int prereq)
            {
                string script = RecoverBeat.ItemAddedScript(build.recoversHooked);
                build.recoversHooked++;
                var sc = StageHook(script, build.beatStage[b], prereq);
                var fp = new ScriptObjectProperty { Name = "ItemFilter", Flags = ScriptProperty.Flag.Edited };
                fp.Object.SetTo(build.itemOf[b]);
                sc.Properties.Add(fp);
                return build.AttachBeatHook(b, build.t.playerAlias, script, sc, prereq);
            }
        }

        /// <summary>A craft's [item, count] pairs, read one by one: (key, count, null) or (null, 0, why it cannot be read).</summary>
        private static IEnumerable<(string? key, int count, string? err)> ReadIngredients(Craft c)
        {
            foreach (var pair in c.ingredients ?? new List<List<JsonElement>>())
            {
                if (pair.Count != 2 || pair[0].ValueKind != JsonValueKind.String || pair[1].ValueKind != JsonValueKind.Number
                    || !pair[1].TryGetInt32(out int n) || n < 1)
                { yield return (null, 0, $"an ingredient must be [\"name\", count >= 1], not [{string.Join(", ", pair.Select(e => e.GetRawText()))}]"); continue; }
                yield return (pair[0].GetString(), n, null);
            }
        }

        /// <summary>
        /// One ingredient, as a Resource in the load order: by EditorID first, else by its in-game name if exactly ONE
        /// item carries it. Ambiguous or missing is null with the reason, never a guess (the needle's worst answer is a
        /// plausible wrong record).
        /// </summary>
        private static IResourceGetter? ResolveIngredient(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, HashSet<Mutagen.Bethesda.Plugins.ModKey> allowed,
                                                          string key, out string why)
        {
            // Own-or-master only: a record from a plugin that is not a master would make it one (OwnOrMaster's rule).
            // A RESOURCE (IRES, the Res* forms), never a MiscItem: every component of his MB11a's COBJ is one, and the
            // first build resolved "Aluminum" to the MiscItem InorgCommonAluminum, which the bench then GAVE him rather
            // than charged (his play, 2026-10-26: "The resource costs are negative, It gives copper").
            var all = env.LoadOrder.PriorityOrder.Resource().WinningOverrides().Where(m => allowed.Contains(m.FormKey.ModKey)).ToList();
            var byId = all.FirstOrDefault(m => string.Equals(m.EditorID, key, StringComparison.OrdinalIgnoreCase));
            if (byId != null) { why = ""; return byId; }
            var byName = all.Where(m => string.Equals(m.Name?.String, key, StringComparison.OrdinalIgnoreCase)).ToList();
            if (byName.Count == 1) { why = ""; return byName[0]; }
            why = byName.Count == 0
                ? "is neither a Resource (Res*) EditorID nor any Resource's name in the plugin or its masters"
                : $"names {byName.Count} Resources ({string.Join(", ", byName.Take(6).Select(m => m.EditorID))}); give the EditorID";
            return null;
        }
    }
}
