using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;

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

            Console.WriteLine(fail == 0 ? "selftest: all passed" : $"selftest: {fail} FAILED");
            return fail == 0 ? 0 : 1;
        }
    }
}
