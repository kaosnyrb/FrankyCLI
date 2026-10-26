using System.Collections.Generic;

namespace FrankyCLI
{
    /// <summary>
    /// THE QUEST MODEL: every field of a QUST record that this tool can write, as plain data. A FormKey is
    /// "XXXXXX:plugin.esm"; a byte field is hex; an enum is its raw number, so a value the enum has no name
    /// for still round-trips. Nothing here is optional by accident: a field the reader cannot place in this
    /// model is a REFUSAL, never a drop (see gen_questmodel_read.cs).
    /// </summary>
    public sealed class QModel
    {
        public string FormKey { get; set; } = "";
        public QHeader Header { get; set; } = new();
        public string? EditorID { get; set; }
        public string? Name { get; set; }
        public QData Data { get; set; } = new();
        /// The story-manager start event as its raw four bytes: a present-but-empty event (07a) has no string form.
        public int? Event { get; set; }
        public string? QuestType { get; set; }
        public string? QuestFaction { get; set; }
        public string? QuestGroup { get; set; }
        public string? Location { get; set; }
        public string? QuestTimeLimit { get; set; }
        public string? SourceQuest { get; set; }
        public string? MissionTypeKeyword { get; set; }
        public string? MissionBoardDescription { get; set; }
        public string? Filter { get; set; }
        public string? Summary { get; set; }
        public string? SwfFile { get; set; }
        public string? ScriptComment { get; set; }
        public int Timestamp { get; set; }
        public int Unknown { get; set; }
        public List<string>? Keywords { get; set; }
        public List<string> TextDisplayGlobals { get; set; } = new();
        public List<QPanel>? MissionBoardInfoPanels { get; set; }
        /// Present-but-empty is real here: vanilla writes a bare NEXT marker for an empty UnusedConditions.
        public List<QCondition>? DialogConditions { get; set; }
        public List<QCondition>? UnusedConditions { get; set; }
        public List<QStage> Stages { get; set; } = new();
        public List<QObjective> Objectives { get; set; } = new();
        public List<QAlias> Aliases { get; set; } = new();
        public QVmad? Vmad { get; set; }
        /// The quest's own child records (scenes, topics with their INFOs), written in the quest's group.
        public QChildren? Children { get; set; }
    }

    /// The record header: the engine reads FormVersion to know how to parse the record, so it is data, not noise.
    public sealed class QHeader
    {
        public int MajorFlags { get; set; }
        public ushort FormVersion { get; set; }
        public ushort Version2 { get; set; }
        public uint VersionControl { get; set; }
    }

    public sealed class QData
    {
        public uint Flags { get; set; }
        public byte Priority { get; set; }
        public int Type { get; set; }
        public string Unused { get; set; } = "";
    }

    public sealed class QStage
    {
        public ushort Index { get; set; }
        public int Flags { get; set; }
        public byte Unknown { get; set; }
        public List<QLogEntry> LogEntries { get; set; } = new();
    }

    public sealed class QLogEntry
    {
        public int? Flags { get; set; }
        public string? Entry { get; set; }
        public string? Note { get; set; }
        public string? ScriptFlagComment { get; set; }
        public List<QCondition> Conditions { get; set; } = new();
        public List<QStageComplete> StageCompleteDatas { get; set; } = new();
    }

    public sealed class QStageComplete
    {
        public string? QSRD { get; set; }
        public string? AffinityChange { get; set; }
        public List<QReward> RewardDatas { get; set; } = new();
    }

    public sealed class QReward
    {
        public string? XpAwarded { get; set; }
        public string? BonusCredits { get; set; }
        public List<QRewardItem> Rewards { get; set; } = new();
        public List<QCondition> Conditions { get; set; } = new();
    }

    public sealed class QRewardItem { public string Item { get; set; } = ""; public uint Count { get; set; } }

    public sealed class QObjective
    {
        public ushort Index { get; set; }
        public int? Flags { get; set; }
        public string? DisplayText { get; set; }
        public List<QTarget> Targets { get; set; } = new();
    }

    public sealed class QTarget
    {
        public int AliasID { get; set; }
        public int Flags { get; set; }
        public string? Keyword { get; set; }
        public int DataTypeState { get; set; }
        public List<QCondition> Conditions { get; set; } = new();
    }

    /// One alias. Kind is "ref" or "loc"; a field that does not apply to the kind stays null.
    public sealed class QAlias
    {
        public string Kind { get; set; } = "";
        public uint ID { get; set; }
        public string? Name { get; set; }
        public uint? Flags { get; set; }
        public long? ALFG { get; set; }
        public int? AliasIDToForceIntoWhenFilled { get; set; }
        public int? ClosestToAlias { get; set; }
        public string? DisplayName { get; set; }
        public List<QCondition>? Conditions { get; set; }
        // ref
        public byte? LegendaryRank { get; set; }
        public string? ForcedReference { get; set; }
        public string? UniqueActor { get; set; }
        public string? UniqueBaseForm { get; set; }
        public QInLocation? InLocation { get; set; }
        public QCreate? Create { get; set; }
        public QExternal? External { get; set; }
        public int? ReferenceCollectionAliasID { get; set; }
        /// <summary>A ref alias's keyword links to other aliases (a cell's map marker carries one, 842 of 1,029 vanilla
        /// ones; read 2026-10-22 for the orbit place). Null = none.</summary>
        public List<QLinkedAlias>? LinkedAliases { get; set; }
        public List<string>? Keywords { get; set; }
        public string? DeathItem { get; set; }
        public string? ForcedVoice { get; set; }
        /// VTCK. Every Delve ref alias carries it present-and-zero, so "absent" and "zero" are different records here.
        public string? VoiceTypes { get; set; }
        /// The packages a ref alias puts on whatever fills it (a wave's travel package rides on the collection member).
        public List<string>? PackageData { get; set; }
        // loc
        public string? SpecificLocation { get; set; }
        public string? LocationTypeKeyword { get; set; }
        public int? SystemLocationAliasID { get; set; }
        public int? ParentSystemLocationAliasID { get; set; }
        /// A PCM request (a place made for the quest rather than found): its own conditions and the content keyword.
        public QAlps? Alps { get; set; }
        // coll: a collection alias is a list of members, each wrapping a ref alias of its own
        public List<QCollectionMember>? Collection { get; set; }
    }

    public sealed class QAlps { public List<QCondition>? Conditions { get; set; } public string? PcmTypeKeyword { get; set; } }

    public sealed class QCollectionMember
    {
        public uint? ID { get; set; }
        public byte MaxInitialFillCount { get; set; }
        public int? ALAM { get; set; }
        public QAlias? ReferenceAlias { get; set; }
    }

    public sealed class QPanel { public string? Header { get; set; } public string? Line1 { get; set; } public string? Line2 { get; set; } }

    public sealed class QInLocation { public int? AliasID { get; set; } public string? Keyword { get; set; } public string? RefType { get; set; } }
    public sealed class QCreate { public string Object { get; set; } = ""; public short AliasID { get; set; } public int Create { get; set; } public int Level { get; set; } }
    public sealed class QExternal { public string? Quest { get; set; } public int? AliasID { get; set; } }
    public sealed class QLinkedAlias { public string? Keyword { get; set; } public int AliasID { get; set; } }

    /// A condition row. Function is the ConditionData class name less "ConditionData"; only the functions in
    /// gen_questmodel's table can be written, and any other is refused on read.
    public sealed class QCondition
    {
        public string Kind { get; set; } = "";          // "float" | "global"
        public string Function { get; set; } = "";
        public int CompareOperator { get; set; }
        public int Flags { get; set; }
        public float? Value { get; set; }
        public string? Global { get; set; }
        public string Unknown1 { get; set; } = "";
        public ushort Unknown2 { get; set; }
        public int RunOnType { get; set; }
        public string? Reference { get; set; }
        public int Unknown3 { get; set; }
        public bool UseAliases { get; set; }
        public bool UsePackageData { get; set; }
        public string? FirstLink { get; set; }
        public uint? FirstIndex { get; set; }
        public string? FirstUnusedString { get; set; }
        public int SecondParameter { get; set; }
        public string? SecondUnusedString { get; set; }
        /// Functions whose FIRST parameter is a plain int and whose SECOND is the link-or-index
        /// (IsInSameCurrentLocationAsAlias): FirstInt carries the first, SecondLink/SecondIndex the second.
        public int? FirstInt { get; set; }
        public string? SecondLink { get; set; }
        public uint? SecondIndex { get; set; }
    }

    public sealed class QVmad
    {
        public short Version { get; set; }
        public ushort ObjectFormat { get; set; }
        public byte ExtraBindDataVersion { get; set; }
        public int Versioning { get; set; }
        public QScript? FragmentScript { get; set; }
        public List<QFragment> Fragments { get; set; } = new();
        public List<QScript> Scripts { get; set; } = new();
        public List<QAliasScripts> Aliases { get; set; } = new();
    }

    public sealed class QFragment
    {
        public ushort Stage { get; set; }
        public short Unknown { get; set; }
        public int StageIndex { get; set; }
        public sbyte Unknown2 { get; set; }
        public string? ScriptName { get; set; }
        public string? FragmentName { get; set; }
    }

    public sealed class QScript
    {
        public string Name { get; set; } = "";
        public int Flags { get; set; }
        public List<QProperty> Properties { get; set; } = new();
    }

    public sealed class QAliasScripts
    {
        public short Version { get; set; }
        public ushort ObjectFormat { get; set; }
        public QProperty Property { get; set; } = new();
        public List<QScript> Scripts { get; set; } = new();
    }

    /// A Papyrus property. Type is the Mutagen class less "Script" and "Property": Int, Float, Bool, String,
    /// Object, IntList, FloatList, BoolList, StringList, ObjectList, StructList. Anything else (a single Struct) is refused
    /// on read. A StructList is a list of structs, each its members as properties (vanilla's
    /// DefaultQuestChangeLocationScript.ChangeLocationStages, first met on his MB01a 2026-10-26).
    public sealed class QProperty
    {
        public string Type { get; set; } = "";
        public string Name { get; set; } = "";
        public int Flags { get; set; }
        public int? Int { get; set; }
        public float? Float { get; set; }
        public bool? Bool { get; set; }
        public string? String { get; set; }
        public QObjectRef? Object { get; set; }
        public List<int>? IntList { get; set; }
        public List<float>? FloatList { get; set; }
        public List<bool>? BoolList { get; set; }
        public List<string>? StringList { get; set; }
        public List<QObjectRef>? ObjectList { get; set; }
        public List<List<QProperty>>? StructList { get; set; }
    }

    public sealed class QObjectRef { public string? Form { get; set; } public short Alias { get; set; } public ushort Unused { get; set; } }
}
