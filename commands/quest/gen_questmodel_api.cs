using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    /// <summary>
    /// THE SURFACE OTHER COMMANDS USE. gen_delve builds through these calls and nothing else of this command:
    /// a model read off a quest (or off one alias), a model to JSON and back, a quest (or an alias) written from a model,
    /// and a SHELL applied to a new quest. Everything behind them (the reader's guard, the writer's tables) stays
    /// private to gen_questmodel.
    /// </summary>
    public static partial class gen_questmodel
    {
        /// The model of a quest. An empty refusal list means every field of the record is in the model.
        internal static QModel ReadModel(IQuestGetter q, out List<string> refusals)
        {
            var rd = new Reader();
            var m = rd.Quest(q);
            refusals = rd.Refusals;
            return m;
        }

        internal static string ToJson(QModel m) => JsonSerializer.Serialize(m, Json);
        internal static QModel FromJson(string json) => JsonSerializer.Deserialize<QModel>(json, Json)
            ?? throw new InvalidOperationException("the model file is empty");

        /// A quest built from a model alone, every object new.
        internal static Quest WriteModel(QModel m) => WriteQuest(m);

        /// ONE ALIAS as a model: a part a build takes off ANOTHER quest (gen_delve's parts file), read with the same guard
        /// as a whole quest, so a field the model cannot hold is a refusal naming its path, never a drop.
        internal static QAlias ReadAlias(IAQuestAliasGetter a, string path, out List<string> refusals)
        {
            var rd = new Reader();
            var m = rd.AliasOf(a, path);
            refusals = rd.Refusals;
            return m;
        }

        /// An alias written from its model, every object new: QuestReferenceAlias, QuestLocationAlias or QuestCollectionAlias.
        internal static AQuestAlias WriteAliasModel(QAlias a) => WriteAlias(a);

        /// The options every model file is written with, so a file holding models beside other data reads the same.
        internal static JsonSerializerOptions JsonOptions => Json;

        private sealed partial class Reader
        {
            public QAlias AliasOf(IAQuestAliasGetter a, string path) => Alias(a, path);
        }

        /// Do two quests write the same bytes? Each goes into a plugin of its own (a COPY of each, so neither the
        /// caller's objects nor their mod are touched) and the files are compared. The same proof `check` makes.
        internal static bool SameBytes(Quest a, Quest b, out int lenA, out int lenB)
        {
            byte[] x = Standalone((Quest)a.DeepCopy(), "emit-rules");
            byte[] y = Standalone((Quest)b.DeepCopy(), "emit-model");
            lenA = x.Length; lenB = y.Length;
            return x.SequenceEqual(y);
        }

        // ---------------------------------------------------------------- shells
        /// ⭐ THE FIELDS A SHELL CARRIES, and why exactly these: they are the fields gen_delve's build copied off its
        /// base quest before shells existed (its `new Quest(myMod) { ... = copy.X }` block, 2026-10-15). Every other
        /// field of the new quest stayed at its default then and stays at it now, so a shell reproduces that build
        /// exactly. A field outside this list in a shell file is not read.
        /// ⛔ "Components" is in the list and NOT in the model: the clone copied it, so a base carrying any is a REFUSAL
        /// (the shell could not reproduce it), never "left out". It was missing until 2026-10-15, when the board's shell
        /// was about to be written: a non-empty one would have been printed as a field the build never took, which was false.
        internal static readonly string[] ShellFields =
        {
            "Name", "Data", "Keywords", "Location", "QuestType", "MissionTypeKeyword", "Summary",
            "MissionBoardDescription", "MissionBoardInfoPanels", "Stages", "Objectives", "Aliases", "VirtualMachineAdapter", "MajorFlags",
            "Components",
        };

        /// A shell model: the base quest's model with only ShellFields kept. FormKey and EditorID stay as the base's
        /// so the build can repoint links to "the base" at the new quest, exactly as it did for a clone.
        internal static QModel ShellOf(QModel full) => new()
        {
            FormKey = full.FormKey,
            EditorID = full.EditorID,
            Header = new QHeader { MajorFlags = full.Header.MajorFlags },
            Name = full.Name,
            Data = full.Data,
            Keywords = full.Keywords,
            Location = full.Location,
            QuestType = full.QuestType,
            MissionTypeKeyword = full.MissionTypeKeyword,
            Summary = full.Summary,
            MissionBoardDescription = full.MissionBoardDescription,
            MissionBoardInfoPanels = full.MissionBoardInfoPanels,
            Stages = full.Stages,
            Objectives = full.Objectives,
            Aliases = full.Aliases,
            Vmad = full.Vmad,
        };

        /// A refusal matters to a shell only if it lands in a field the shell carries.
        internal static bool InShell(string refusal) =>
            refusal.StartsWith("Data") || refusal.StartsWith("Stages") || refusal.StartsWith("Objectives")
            || refusal.StartsWith("Aliases") || refusal.StartsWith("VMAD") || refusal.StartsWith("MissionBoardInfoPanels")
            || ShellFields.Any(f => refusal.StartsWith("Quest." + f + " ") || refusal.StartsWith("Quest." + f + " ="));

        /// Fill a NEW quest from a shell: only ShellFields, every object new. The quest keeps its own FormKey, header
        /// and EditorID; everything outside the shell stays at its default.
        internal static void ApplyShell(Quest q, QModel s)
        {
            q.MajorFlags = (Quest.MajorFlag)s.Header.MajorFlags;
            if (s.Name != null) q.Name = s.Name;
            q.Data = new QuestData
            {
                Flags = En<Quest.Flag>(s.Data.Flags), Priority = s.Data.Priority,
                Type = En<Quest.TypeEnum>(s.Data.Type), Unused = Bytes(s.Data.Unused),
            };
            if (s.Keywords != null) q.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>(s.Keywords.Select(k => (IFormLinkGetter<IKeywordGetter>)new FormLink<IKeywordGetter>(Key(k))));
            Link(q.Location, s.Location); Link(q.QuestType, s.QuestType); Link(q.MissionTypeKeyword, s.MissionTypeKeyword);
            q.Summary = s.Summary;
            if (s.MissionBoardDescription != null) q.MissionBoardDescription = s.MissionBoardDescription;
            if (s.MissionBoardInfoPanels != null)
                foreach (var p in s.MissionBoardInfoPanels)
                {
                    var panel = new QuestMissionBoardPanel();
                    if (p.Header != null) panel.Header = p.Header;
                    if (p.Line1 != null) panel.Line1 = p.Line1;
                    if (p.Line2 != null) panel.Line2 = p.Line2;
                    q.MissionBoardInfoPanels.Add(panel);
                }
            q.Stages.AddRange(s.Stages.Select(WriteStage));
            q.Objectives.AddRange(s.Objectives.Select(WriteObjective));
            q.Aliases = new ExtendedList<AQuestAlias>(s.Aliases.Select(WriteAlias));
            if (s.Vmad != null) q.VirtualMachineAdapter = WriteVmad(s.Vmad);
        }
    }
}
