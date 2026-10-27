using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// `gen_delve selftest`: the build's pure helpers, graded on quests made in memory, with no plugin and no game
        /// data loaded. Exit 1 on any failure. Each case was bitten on the real previous version of the code it guards.
        /// </summary>
        private static int SelfTest()
        {
            int fail = 0;
            void Check(string label, bool ok, string detail)
            {
                Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label + "   " + detail);
                if (!ok) fail++;
            }
            uint next = 0x800;
            Quest QuestOf(params AQuestAlias[] aliases)
            {
                var q = new Quest(new FormKey(ModKey.FromNameAndExtension("SelfTest.esm"), next++), StarfieldRelease.Starfield)
                { Aliases = new ExtendedList<AQuestAlias>() };
                q.Aliases.AddRange(aliases);
                return q;
            }
            // Mutagen types Name as non-null and reads a missing name as empty; a null one is planted on purpose (`!`),
            // because code and a model JSON carrying "Name": null are the only ways one arises, and the old filter
            // skipped exactly that.
            QuestReferenceAlias Ref(uint id, string? name) => new() { ID = id, Name = name! };
            QuestLocationAlias Loc(uint id, string? name) => new() { ID = id, Name = name! };
            QuestCollectionAlias Coll(uint id, string? name)
            {
                var c = new QuestCollectionAlias();
                c.Collection.Add(new CollectionAlias { ID = id, ReferenceAlias = Ref(id, name) });
                return c;
            }

            // NextAliasId and AliasTaken count EVERY alias, whatever its name (until 2026-10-15 a null-named one was
            // skipped, so its id could be handed out twice).
            var named = QuestOf(Ref(5, "A"), Loc(7, "B"));
            Check("control: named aliases only, next is one past the top", NextAliasId(named) == 8, $"next {NextAliasId(named)}");
            var emptyName = QuestOf(Ref(5, "A"), Ref(9, ""));
            Check("control: an empty name (how Mutagen reads a missing one) counts", NextAliasId(emptyName) == 10, $"next {NextAliasId(emptyName)}");
            var nullRef = QuestOf(Ref(5, "A"), Ref(9, null));
            Check("a null-named ref alias on top counts", NextAliasId(nullRef) == 10, $"next {NextAliasId(nullRef)}");
            var nullLoc = QuestOf(Ref(5, "A"), Loc(11, null));
            Check("a null-named location alias on top counts", NextAliasId(nullLoc) == 12, $"next {NextAliasId(nullLoc)}");
            var nullMember = QuestOf(Ref(5, "A"), Coll(12, null));
            Check("a null-named collection member on top counts", NextAliasId(nullMember) == 13, $"next {NextAliasId(nullMember)}");
            Check("an empty quest's first alias is 1", NextAliasId(QuestOf()) == 1, $"next {NextAliasId(QuestOf())}");
            Check("an id a null-named alias holds is taken", AliasTaken(nullRef, 9), "id 9");
            Check("control: a free id is not taken", !AliasTaken(nullRef, 6), "id 6");

            // recover's OnItemAdded hook per recover on the player alias: the base, then A-D. No recipe has two recovers,
            // so no build has taken a suffix (split step (d2) named it; pinned at (d3)).
            string[] itemAdded = { "DefaultAliasOnItemAddedScript", "DefaultAliasOnItemAddedScriptA", "DefaultAliasOnItemAddedScriptB",
                                   "DefaultAliasOnItemAddedScriptC", "DefaultAliasOnItemAddedScriptD" };
            for (int n = 0; n < itemAdded.Length; n++)
                Check($"recover {n + 1}'s hook is {itemAdded[n]}", RecoverBeat.ItemAddedScript(n) == itemAdded[n], RecoverBeat.ItemAddedScript(n));

            // The naming rule (delve_name.cs, 2026-10-27): the scope read off kind and deal, the next family and letter.
            string S(string? kind, string? deal) => ScopeOf(kind, deal, out _)?.Label ?? "none";
            Check("a board template is a board", S("board", "_duo_MB_MissionNode_test") == "board", S("board", "_duo_MB_MissionNode_test"));
            Check("boardspace is a board", S("boardspace", null) == "board", S("boardspace", null));
            Check("slatespace is remote", S("slatespace", null) == "remote", S("slatespace", null));
            Check("LocalCargo is not read as Local", S("beats", "duo_MS_ArtifactMap_LocalCargoNode_Rare") == "localcargo", S("beats", "duo_MS_ArtifactMap_LocalCargoNode_Rare"));
            Check("a Local test node is local", S("beats", "_duo_MS_ArtifactMapLocal_TEST") == "local", S("beats", "_duo_MS_ArtifactMapLocal_TEST"));
            Check("RemotePlanet is remote_planet", S("beats", "duo_MS_ArtifactMap_RemotePlanetNode") == "remote_planet", S("beats", "duo_MS_ArtifactMap_RemotePlanetNode"));
            Check("a beats recipe with no deal has no scope", S("beats", null) == "none", S("beats", null));
            Check("the test ladder has no scope", S("space", null) == "none", S("space", null));
            var mb = ScopeOf("board", "x", out _)!;
            string? N(IEnumerable<string> ids, int? fam = null) => NextName(mb, ids, fam, out _);
            var have = new[] { "duo_MB01a_qst", "duo_MB31a_qst", "duo_MB40a_qst", "duo_MB47a_qst", "duo_MB50a_qst", "duo_delve_missionboard28a" };
            Check("the next family is one past the highest Delve", N(have) == "duo_MB51a_qst", N(have) ?? "null");
            Check("an empty scope starts at the floor", N(new[] { "duo_MB31a_qst" }) == "duo_MB40a_qst", N(new[] { "duo_MB31a_qst" }) ?? "null");
            Check("a family's next letter", N(have.Append("duo_MB47b_qst"), 47) == "duo_MB47c_qst", N(have.Append("duo_MB47b_qst"), 47) ?? "null");
            Check("a classic family is refused", N(have, 31) == null, N(have, 31) ?? "null");
            Check("a family with no member is refused", N(have, 48) == null, N(have, 48) ?? "null");
            var lc = ScopeOf("beats", "duo_MS_ArtifactMap_LocalCargoNode", out _)!;
            string? L(params string[] ids) => NextName(lc, ids, null, out _);
            Check("a slate scope numbers from 20 and ignores other scopes", L("duo_artifact_localcargo_qst09a", "duo_artifact_local_qst29a") == "duo_artifact_localcargo_qst20a",
                  L("duo_artifact_localcargo_qst09a", "duo_artifact_local_qst29a") ?? "null");
            Check("a draft is a draft", IsDraftId("duo_delve_missionboard28a") && IsDraftId("duo_delve36a"), "duo_delve...");
            Check("a test rung is not a draft", !IsDraftId("duo_delvetest_bs1"), "duo_delvetest_bs1");
            Check("a named Delve is not a draft", !IsDraftId("duo_MB51a_qst"), "duo_MB51a_qst");

            Console.WriteLine(fail == 0 ? "selftest: all passed" : $"selftest: {fail} FAILED");
            return fail == 0 ? 0 : 1;
        }
    }
}
