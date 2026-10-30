using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// `gen_delve factions [--dry]`: write the faction table (data/delves/factions.json) into the plugin as two
        /// PARALLEL FormLists the runtime reads: the sites' faction keywords and, at the same index, the wave list for
        /// that faction. Plan OT-01, his 2026-10-28 "would be cool to match factions" and the 2026-10-30 probe (manual
        /// Inbox, for 32): a board site's faction is ONE LocEnc*_Exclusive keyword left on the created location at the
        /// listing, the residents follow it, and swapping it before the site loads changes them.
        /// The table is DATA so another mod's faction is a row, never code (his "there is mods which add new POI factions
        /// as well"). The Delve build only READS these two lists; this verb is the one writer, proven off disk.
        /// </summary>
        private static int Factions(string[] args, string dataDir, List<Template> templates, bool dry)
        {
            var mods = templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; factions knows which plugin only when they agree."); return 1; }
            var table = LoadFactionTable(dataDir);
            if (table == null) return 1;

            // Resolve every row against the plugin and its masters before anything opens for writing.
            var kws = new List<FormKey>(); var gangs = new List<FormKey>();
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                var allowed = MasterSet(env, mods[0]);
                int fail = 0;
                foreach (var f in table.factions)
                {
                    var kw = OwnOrMaster<IKeywordGetter>(allowed, env, f.keyword);
                    var gl = OwnOrMaster<IFormListGetter>(allowed, env, f.gang);
                    if (kw == null) { Console.WriteLine($"REFUSED: {f.name}.keyword '{f.keyword}' is not a Keyword in {mods[0]} or a master."); fail++; }
                    if (gl == null) { Console.WriteLine($"REFUSED: {f.name}.gang '{f.gang}' is not a FormList in {mods[0]} or a master."); fail++; }
                    else if (gl.Items.Count == 0) { Console.WriteLine($"REFUSED: {f.name}.gang '{f.gang}' is EMPTY, so it would spawn nothing."); fail++; }
                    if (kw != null && gl != null)
                    {
                        kws.Add(kw.FormKey); gangs.Add(gl.FormKey);
                        Console.WriteLine($"  {f.name,-14} {kw.EditorID} [{kw.FormKey}]  ->  {gl.EditorID} [{gl.FormKey}] ({gl.Items.Count} entries)");
                    }
                }
                if (fail > 0) return 1;
            }

            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));

            bool changed = false;
            FormKey Put(string edid, List<FormKey> items)
            {
                var flst = myMod.FormLists.FirstOrDefault(x => string.Equals(x.EditorID, edid, StringComparison.OrdinalIgnoreCase));
                bool same = flst != null && flst.Items.Select(i => i.FormKey).SequenceEqual(items);
                if (same) { Console.WriteLine($"  {edid}: current ({items.Count} entries), unchanged"); return flst!.FormKey; }
                if (flst == null) { flst = new FormList(myMod) { EditorID = edid }; myMod.FormLists.Add(flst); Console.WriteLine($"  {edid}: NEW"); }
                else Console.WriteLine($"  {edid}: items replaced ({flst.Items.Count} -> {items.Count})");
                flst.Items.Clear();
                foreach (var k in items) flst.Items.Add(new FormLink<IStarfieldMajorRecordGetter>(k));
                changed = true;
                return flst.FormKey;
            }
            Console.WriteLine();
            Console.WriteLine($"=== FACTIONS: {table.factions.Count} row(s) ===");
            var kwKey = Put(table.keywordList, kws);
            var gangKey = Put(table.gangList, gangs);
            if (!changed) { Console.WriteLine("  nothing to write."); return 0; }
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath);
            using var reread = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            bool Check(FormKey key, List<FormKey> want, string edid)
            {
                var got = reread.FormLists.FirstOrDefault(x => x.FormKey == key);
                bool ok = got != null && got.EditorID == edid && got.Items.Select(i => i.FormKey).SequenceEqual(want);
                Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] off disk: {edid} [{key}] holds the table's {want.Count} entries, in order");
                return ok;
            }
            bool okK = Check(kwKey, kws, table.keywordList), okG = Check(gangKey, gangs, table.gangList);
            var mastersAfter = string.Join(", ", reread.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            Console.WriteLine($"  [{(mastersAfter == mastersBefore ? "OK  " : "FAIL")}] the plugin's masters are unchanged ({mastersAfter})");
            return okK && okG && mastersAfter == mastersBefore ? 0 : 1;
        }

        /// <summary>
        /// data/delves/factions.json, refused loudly at the boundary: unknown fields, no rows, a name twice, a member gate
        /// naming a row that is not there. The ONE loader, for the verb, the lint and the build alike.
        /// </summary>
        private static FactionTable? LoadFactionTable(string? dataDir = null)
        {
            dataDir ??= ResolveDataDir();
            if (dataDir == null) { Console.WriteLine("REFUSED: no data/delves directory to find factions.json in."); return null; }
            string path = Path.Combine(dataDir, "factions.json");
            if (!File.Exists(path)) { Console.WriteLine($"REFUSED: no {path}."); return null; }
            FactionTable? table;
            try { table = JsonSerializer.Deserialize<FactionTable>(File.ReadAllText(path), StrictJsonOpts); }
            catch (JsonException e) { Console.WriteLine($"REFUSED: {path}: {e.Message}"); return null; }
            if (table == null || table.factions.Count == 0) { Console.WriteLine("REFUSED: factions.json names no faction."); return null; }
            if (string.IsNullOrWhiteSpace(table.keywordList) || string.IsNullOrWhiteSpace(table.gangList))
            { Console.WriteLine("REFUSED: factions.json needs keywordList and gangList (the two FormLists' EditorIDs)."); return null; }
            var dupe = table.factions.GroupBy(f => f.name, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (dupe != null) { Console.WriteLine($"REFUSED: faction '{dupe.Key}' is named twice."); return null; }
            var gate = table.memberGate;
            if (gate != null && (string.IsNullOrWhiteSpace(gate.faction)
                || !table.factions.Any(f => string.Equals(f.name, gate.row, StringComparison.OrdinalIgnoreCase))
                || !table.factions.Any(f => string.Equals(f.name, gate.instead, StringComparison.OrdinalIgnoreCase))))
            { Console.WriteLine("REFUSED: factions.json memberGate needs a faction, and a row and an instead that are both rows of the table."); return null; }
            return table;
        }

        private static HashSet<ModKey> MasterSet(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string mod)
        {
            var key = ModKey.FromNameAndExtension(mod + ".esm");
            var me = env.LoadOrder.PriorityOrder.FirstOrDefault(l => l.ModKey == key)?.Mod;
            var set = new HashSet<ModKey> { key };
            if (me != null) foreach (var m in me.ModHeader.MasterReferences) set.Add(m.Master);
            return set;
        }

        private static readonly JsonSerializerOptions StrictJsonOpts = new()
        {
            PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true,
            UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow,
        };

        private sealed class FactionTable
        {
            public int schema { get; set; }
            public List<string>? _note { get; set; }
            public string keywordList { get; set; } = "";
            public string gangList { get; set; } = "";
            public List<FactionRow> factions { get; set; } = new();
            public MemberGate? memberGate { get; set; }
        }

        /// <summary>A joinable faction (his 10-28: "the player can join the crimson fleet. Generally itd be good to prevent
        /// those missions starting"): for a member, a site rolled as `row` is swapped to `instead` at the accept.</summary>
        private sealed class MemberGate
        {
            public string faction { get; set; } = "";
            public string row { get; set; } = "";
            public string instead { get; set; } = "";
            public string? _note { get; set; }
        }

        private sealed class FactionRow
        {
            public string name { get; set; } = "";
            public string keyword { get; set; } = "";
            public string gang { get; set; } = "";
        }
    }
}
