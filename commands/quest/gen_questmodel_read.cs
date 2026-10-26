using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda.Strings;
using Noggog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace FrankyCLI
{
    /// <summary>
    /// READ A QUST INTO THE MODEL, AND REFUSE WHAT THE MODEL CANNOT HOLD.
    ///
    /// ⭐ Each level names the fields it carries, and a guard walks the REST of that level's getter interface:
    /// any field with a value that no line here carried is a refusal naming its path. So the model can only
    /// ever be incomplete OUT LOUD. A silent drop here would be invisible to the writer and would come back as
    /// a record that is merely "nearly" the same, which is the failure this whole command exists to rule out.
    /// The byte comparison in gen_questmodel check is the second, independent net under this one.
    /// </summary>
    public static partial class gen_questmodel
    {
        private sealed partial class Reader
        {
            public readonly List<string> Refusals = new();
            public string Children = "";
            /// --no-children: compare the QUST record alone, the way the first version of check did.
            public bool SkipChildren;

            /// Fields every level may carry without the model holding them: identity and Mutagen's own plumbing.
            private static readonly HashSet<string> Plumbing = new()
            {
                "Registration", "StaticRegistration", "FormKey", "FormVersion", "VersionControl", "IsCompressed",
                "IsDeleted", "MajorRecordFlagsRaw", "Version2", "EditorID", "TitleString", "Type", "IsDisabled",
                "ContainedFormLinks", "EnumerateFormLinks", "StarfieldMajorRecordFlags", "MajorFlags", "BinaryWriteTranslator",
            };

            // ------------------------------------------------------------ the guard
            private void Guard(object o, Type iface, string path, params string[] carried)
            {
                var keep = new HashSet<string>(carried);
                foreach (var p in PropsOf(iface))
                {
                    if (keep.Contains(p.Name) || p.Name is "StaticRegistration" or "Registration" || (Plumbing.Contains(p.Name) && IsMajor(iface))) continue;
                    if (p.GetIndexParameters().Length > 0) continue;
                    object? v;
                    try { v = p.GetValue(o); } catch (Exception e) { Refusals.Add($"{path}.{p.Name}: unreadable ({e.GetType().Name})"); continue; }
                    // ⛔ A NULLABLE LIST HAS THREE STATES TOO: absent, present-and-EMPTY, and items. Vanilla writes a bare NEXT
                    // marker for an empty UnusedConditions, and reading empty as absent made 342 Starfield quests 6 bytes
                    // short with nothing refused. So a present list on a property declared nullable is never "empty" here.
                    bool presentNullableList = v is IEnumerable and not string && Nullability.Create(p).ReadState == NullabilityState.Nullable;
                    if (presentNullableList || !IsEmpty(v)) Refusals.Add($"{path}.{p.Name} = {Show(v)}  (no field in the model)");
                }
            }

            private static readonly NullabilityInfoContext Nullability = new();

            private static bool IsMajor(Type iface) => typeof(IMajorRecordGetter).IsAssignableFrom(iface);

            private static IEnumerable<PropertyInfo> PropsOf(Type iface) =>
                new[] { iface }.Concat(iface.GetInterfaces())
                    .Where(t => t.Namespace?.StartsWith("Mutagen.Bethesda") == true)
                    .SelectMany(t => t.GetProperties())
                    .GroupBy(p => p.Name).Select(g => g.First());

            private static bool IsEmpty(object? v)
            {
                switch (v)
                {
                    case null: return true;
                    case string s: return s.Length == 0;
                    case bool b: return !b;
                    case Enum e: return Convert.ToInt64(e) == 0;
                    case IConvertible c when v.GetType().IsPrimitive: return Convert.ToDouble(c) == 0;
                    case IFormLinkGetter n when NullableKey(n).Item1: return NullableKey(n).Item2 == null;   // present-and-zero is NOT empty
                    case IFormLinkGetter l: return l.IsNull;
                    case ITranslatedStringGetter t: return string.IsNullOrEmpty(t.String);
                    case ReadOnlyMemorySlice<byte> m: return m.Length == 0;
                    case IEnumerable en: return !en.Cast<object>().Any();
                }
                var t2 = v.GetType();
                if (t2.IsValueType) return v.Equals(System.Activator.CreateInstance(t2));
                // A nested Mutagen object: empty only if every property on it is.
                return t2.GetProperties().Where(p => p.GetIndexParameters().Length == 0 && p.Name != "Registration" && p.Name != "StaticRegistration")
                         .All(p => { try { return IsEmpty(p.GetValue(v)); } catch { return false; } });
            }

            private static string Show(object? v) => v switch
            {
                IFormLinkGetter l => l.FormKey.ToString(),
                ITranslatedStringGetter t => "\"" + t.String + "\"",
                IEnumerable en when v is not string => $"[{en.Cast<object>().Count()} item(s)]",
                _ => v?.ToString() ?? "null",
            };

            // ------------------------------------------------------------ helpers
            /// ⛔ A NULLABLE LINK HAS THREE STATES: absent (no subrecord), present-and-zero, and a form. 07a's every ref
            /// alias carries VTCK = 0 (present, zero), and reading it as "IsNull" folded the first two together: the
            /// constructed record came out 90 bytes short and the reader could not see why. Absent is JSON null;
            /// present-and-zero is FormKey.Null's own spelling, never a sentinel invented here.
            private static string? Fk(IFormLinkGetter l) => NullableKey(l) is (true, var k)
                ? (k is FormKey f ? f.ToString() : null)
                : (l.FormKey == FormKey.Null ? null : l.FormKey.ToString());   // IsNull is also true for 0xFFFFFFFF (TRDA emotion), which is NOT FormKey.Null

            /// (isNullable, its FormKey?) off the link's own FormKeyNullable. There is no non-generic interface that
            /// carries it, so it is read off the concrete link; a link without it is a plain two-state link.
            private static (bool, FormKey?) NullableKey(IFormLinkGetter l) =>
                l.GetType().GetProperty("FormKeyNullable") is { } p ? (true, (FormKey?)p.GetValue(l)) : (false, null);
            private static string Hex(ReadOnlyMemorySlice<byte> m) => Convert.ToHexString(m.ToArray());
            private static string Hex(ReadOnlyMemorySlice<byte>? m) => m is { } x ? Convert.ToHexString(x.ToArray()) : "";
            /// Three states again: no subrecord (null), present and empty (""), text. An overlay hands an empty NNAM back
            /// as a string object whose String is null, which read as absent and dropped a 1-byte subrecord on 3 vanilla quests.
            private static string? Str(ITranslatedStringGetter? t) => t == null ? null : (t.String ?? "");

            // ------------------------------------------------------------ the quest
            public QModel Quest(IQuestGetter q)
            {
                var m = new QModel
                {
                    FormKey = q.FormKey.ToString(),
                    Header = new QHeader { MajorFlags = q.MajorRecordFlagsRaw, FormVersion = q.FormVersion, Version2 = q.Version2, VersionControl = q.VersionControl },
                    EditorID = q.EditorID,
                    Name = Str(q.Name),
                    Data = new QData { Flags = (uint)q.Data.Flags, Priority = q.Data.Priority, Type = (int)q.Data.Type, Unused = Hex(q.Data.Unused) },
                    Event = q.Event?.TypeInt,
                    QuestType = Fk(q.QuestType), QuestFaction = Fk(q.QuestFaction), QuestGroup = Fk(q.QuestGroup),
                    Location = Fk(q.Location), QuestTimeLimit = Fk(q.QuestTimeLimit), SourceQuest = Fk(q.SourceQuest),
                    MissionTypeKeyword = Fk(q.MissionTypeKeyword), MissionBoardDescription = Str(q.MissionBoardDescription),
                    Filter = q.Filter, Summary = q.Summary, SwfFile = q.SwfFile, ScriptComment = q.ScriptComment,
                    Timestamp = q.Timestamp, Unknown = q.Unknown,
                    Keywords = q.Keywords?.Select(k => k.FormKey.ToString()).ToList(),
                    TextDisplayGlobals = q.TextDisplayGlobals.Select(k => k.FormKey.ToString()).ToList(),
                    MissionBoardInfoPanels = q.MissionBoardInfoPanels?.Select((x, i) =>
                    {
                        Guard(x, typeof(IQuestMissionBoardPanelGetter), $"MissionBoardInfoPanels[{i}]", "Header", "Line1", "Line2");
                        return new QPanel { Header = Str(x.Header), Line1 = Str(x.Line1), Line2 = Str(x.Line2) };
                    }).ToList(),
                };
                Guard(q.Data, typeof(IQuestDataGetter), "Data", "Flags", "Priority", "Type", "Unused");
                Guard(q, typeof(IQuestGetter), "Quest", "Name", "Data", "Event", "QuestType", "QuestFaction", "QuestGroup", "Location",
                      "QuestTimeLimit", "SourceQuest", "MissionTypeKeyword", "MissionBoardDescription", "Filter", "Summary", "SwfFile",
                      "ScriptComment", "Timestamp", "Unknown", "Keywords", "TextDisplayGlobals", "Stages", "Objectives", "Aliases",
                      "VirtualMachineAdapter", "Scenes", "DialogTopics", "DialogBranches", "MissionBoardInfoPanels",
                      "DialogConditions", "UnusedConditions");
                m.DialogConditions = q.DialogConditions == null ? null : Conditions(q.DialogConditions, "DialogConditions");
                m.UnusedConditions = q.UnusedConditions == null ? null : Conditions(q.UnusedConditions, "UnusedConditions");
                // ⚠ CHILD RECORDS ARE OUT OF SCOPE FOR NOW, AND SAID SO: a quest's scenes, topics and branches are records of
                // their own in the quest's group, not fields of the QUST record. Counted here so the scope is printed, never
                // dropped in silence; check strips them from BOTH sides and names that it did.
                Children = $"{q.Scenes.Count} scene(s), {q.DialogTopics.Count} topic(s), {q.DialogBranches.Count} branch(es)";

                for (int i = 0; i < q.Stages.Count; i++) m.Stages.Add(Stage(q.Stages[i], $"Stages[{i}]"));
                for (int i = 0; i < q.Objectives.Count; i++) m.Objectives.Add(Objective(q.Objectives[i], $"Objectives[{i}]"));
                for (int i = 0; i < q.Aliases.Count; i++) m.Aliases.Add(Alias(q.Aliases[i], $"Aliases[{i}]"));
                if (q.VirtualMachineAdapter is { } vma) m.Vmad = Vmad(vma, "VMAD");
                m.Children = SkipChildren ? null : ReadChildren(q);
                return m;
            }

            private QStage Stage(IQuestStageGetter s, string path)
            {
                var m = new QStage { Index = s.Index, Flags = (int)s.Flags, Unknown = s.Unknown };
                Guard(s, typeof(IQuestStageGetter), path, "Index", "Flags", "Unknown", "LogEntries");
                for (int i = 0; i < s.LogEntries.Count; i++)
                {
                    var e = s.LogEntries[i]; string p = $"{path}.LogEntries[{i}]";
                    var le = new QLogEntry { Flags = e.Flags is { } f ? (int)f : null, Entry = Str(e.Entry), Note = e.Note, ScriptFlagComment = e.ScriptFlagComment };
                    le.Conditions = Conditions(e.Conditions, p + ".Conditions");
                    for (int k = 0; k < e.StageCompleteDatas.Count; k++)
                    {
                        var sc = e.StageCompleteDatas[k]; string pk = $"{p}.StageCompleteDatas[{k}]";
                        var qs = new QStageComplete { QSRD = sc.QSRD is { } b ? Hex(b) : null, AffinityChange = Fk(sc.AffinityChange) };
                        for (int r = 0; r < sc.RewardDatas.Count; r++)
                        {
                            var rd = sc.RewardDatas[r]; string pr = $"{pk}.RewardDatas[{r}]";
                            qs.RewardDatas.Add(new QReward
                            {
                                XpAwarded = Fk(rd.XpAwarded), BonusCredits = Fk(rd.BonusCredits),
                                Rewards = rd.Rewards.Select(x => new QRewardItem { Item = x.Item.FormKey.ToString(), Count = x.Count }).ToList(),
                                Conditions = Conditions(rd.Conditions, pr + ".Conditions"),
                            });
                            Guard(rd, typeof(IQuestStageRewardDataGetter), pr, "XpAwarded", "BonusCredits", "Rewards", "Conditions");
                        }
                        Guard(sc, typeof(IQuestStageCompleteDataGetter), pk, "QSRD", "AffinityChange", "RewardDatas");
                        le.StageCompleteDatas.Add(qs);
                    }
                    Guard(e, typeof(IQuestLogEntryGetter), p, "Flags", "Entry", "Note", "ScriptFlagComment", "Conditions", "StageCompleteDatas");
                    m.LogEntries.Add(le);
                }
                return m;
            }

            private QObjective Objective(IQuestObjectiveGetter o, string path)
            {
                var m = new QObjective { Index = o.Index, Flags = o.Flags is { } f ? (int)f : null, DisplayText = Str(o.DisplayText) };
                Guard(o, typeof(IQuestObjectiveGetter), path, "Index", "Flags", "DisplayText", "Targets");
                for (int i = 0; i < o.Targets.Count; i++)
                {
                    var t = o.Targets[i]; string p = $"{path}.Targets[{i}]";
                    m.Targets.Add(new QTarget
                    {
                        AliasID = t.AliasID, Flags = (int)t.Flags, Keyword = Fk(t.Keyword), DataTypeState = (int)t.QSTADataTypeState,
                        Conditions = Conditions(t.Conditions, p + ".Conditions"),
                    });
                    Guard(t, typeof(IQuestObjectiveTargetGetter), p, "AliasID", "Flags", "Keyword", "QSTADataTypeState", "Conditions");
                }
                return m;
            }

            private QAlias Alias(IAQuestAliasGetter a, string path)
            {
                switch (a)
                {
                    case IQuestReferenceAliasGetter r:
                    {
                        var m = new QAlias
                        {
                            Kind = "ref", ID = r.ID, Name = r.Name, Flags = r.Flags is { } f ? (uint)f : null, ALFG = r.ALFG,
                            AliasIDToForceIntoWhenFilled = r.AliasIDToForceIntoWhenFilled, ClosestToAlias = r.ClosestToAlias,
                            DisplayName = Fk(r.DisplayName), LegendaryRank = r.LegendaryRank,
                            ForcedReference = Fk(r.ForcedReference), UniqueActor = Fk(r.UniqueActor), UniqueBaseForm = Fk(r.UniqueBaseForm),
                            ReferenceCollectionAliasID = r.ReferenceCollectionAliasID,
                            LinkedAliases = r.LinkedAliases is { Count: > 0 } lk ? lk.Select(x => new QLinkedAlias { Keyword = Fk(x.Keyword), AliasID = x.AliasID }).ToList() : null,
                            Keywords = r.Keywords?.Select(k => k.FormKey.ToString()).ToList(),
                            DeathItem = Fk(r.DeathItem), ForcedVoice = Fk(r.ForcedVoice), VoiceTypes = Fk(r.VoiceTypes),
                            PackageData = r.PackageData is { Count: > 0 } pk ? pk.Select(k => k.FormKey.ToString()).ToList() : null,
                            Conditions = r.Conditions == null ? null : Conditions(r.Conditions, path + ".Conditions"),
                        };
                        if (r.Location is { } l)
                        {
                            m.InLocation = new QInLocation { AliasID = l.AliasID, Keyword = Fk(l.Keyword), RefType = Fk(l.RefType) };
                            Guard(l, typeof(ILocationAliasReferenceGetter), path + ".Location", "AliasID", "Keyword", "RefType");
                        }
                        if (r.CreateReferenceToObject is { } c)
                        {
                            m.Create = new QCreate { Object = c.Object.FormKey.ToString(), AliasID = c.AliasID, Create = (int)c.Create, Level = (int)c.Level };
                            Guard(c, typeof(ICreateReferenceToObjectGetter), path + ".CreateReferenceToObject", "Object", "AliasID", "Create", "Level");
                        }
                        for (int li = 0; li < (r.LinkedAliases?.Count ?? 0); li++)
                            Guard(r.LinkedAliases![li], typeof(ILinkedAliasGetter), $"{path}.LinkedAliases[{li}]", "Keyword", "AliasID");
                        if (r.External is { } x)
                        {
                            m.External = new QExternal { Quest = Fk(x.Quest), AliasID = x.AliasID };
                            Guard(x, typeof(IExternalAliasReferenceGetter), path + ".External", "Quest", "AliasID");
                        }
                        Guard(r, typeof(IQuestReferenceAliasGetter), path, "ID", "Name", "Flags", "ALFG", "AliasIDToForceIntoWhenFilled",
                              "ClosestToAlias", "DisplayName", "LegendaryRank", "ForcedReference", "UniqueActor", "UniqueBaseForm",
                              "ReferenceCollectionAliasID", "LinkedAliases", "Keywords", "DeathItem", "ForcedVoice", "VoiceTypes", "PackageData", "Conditions", "Location",
                              "CreateReferenceToObject", "External");
                        return m;
                    }
                    case IQuestLocationAliasGetter l:
                    {
                        var m = new QAlias
                        {
                            Kind = "loc", ID = l.ID, Name = l.Name, Flags = l.Flags is { } f ? (uint)f : null, ALFG = l.ALFG,
                            AliasIDToForceIntoWhenFilled = l.AliasIDToForceIntoWhenFilled, ClosestToAlias = l.ClosestToAlias,
                            DisplayName = Fk(l.DisplayName), SpecificLocation = Fk(l.SpecificLocation),
                            LocationTypeKeyword = Fk(l.LocationTypeKeyword), SystemLocationAliasID = l.SystemLocationAliasID,
                            ParentSystemLocationAliasID = l.ParentSystemLocationAliasID,
                            Conditions = l.Conditions == null ? null : Conditions(l.Conditions, path + ".Conditions"),
                        };
                        if (l.ALPS is { } alps)
                        {
                            m.Alps = new QAlps
                            {
                                Conditions = alps.Conditions == null ? null : Conditions(alps.Conditions, path + ".ALPS.Conditions"),
                                PcmTypeKeyword = Fk(alps.PcmTypeKeyword),
                            };
                            Guard(alps, typeof(IQuestLocationAliasALPSGetter), path + ".ALPS", "Conditions", "PcmTypeKeyword");
                        }
                        Guard(l, typeof(IQuestLocationAliasGetter), path, "ID", "Name", "Flags", "ALFG", "AliasIDToForceIntoWhenFilled",
                              "ClosestToAlias", "DisplayName", "SpecificLocation", "LocationTypeKeyword", "SystemLocationAliasID",
                              "ParentSystemLocationAliasID", "Conditions", "ALPS");
                        return m;
                    }
                    case IQuestCollectionAliasGetter c:
                    {
                        // A collection has no ID of its own; each member carries one and wraps a whole ref alias.
                        var m = new QAlias { Kind = "coll", Collection = new() };
                        for (int i = 0; i < c.Collection.Count; i++)
                        {
                            var mem = c.Collection[i]; string p = $"{path}.Collection[{i}]";
                            m.Collection.Add(new QCollectionMember
                            {
                                ID = mem.ID, MaxInitialFillCount = mem.MaxInitialFillCount, ALAM = mem.ALAM,
                                ReferenceAlias = mem.ReferenceAlias is { } ra ? Alias(ra, p + ".ReferenceAlias") : null,
                            });
                            Guard(mem, typeof(ICollectionAliasGetter), p, "ID", "MaxInitialFillCount", "ALAM", "ReferenceAlias");
                        }
                        Guard(c, typeof(IQuestCollectionAliasGetter), path, "Collection");
                        return m;
                    }
                    default:
                        Refusals.Add($"{path}: a {a.GetType().Name}, an alias kind the model does not hold yet");
                        return new QAlias { Kind = "unsupported" };
                }
            }

            // ------------------------------------------------------------ conditions
            private List<QCondition> Conditions(IReadOnlyList<IConditionGetter>? cs, string path)
            {
                var outp = new List<QCondition>();
                if (cs == null) return outp;
                for (int i = 0; i < cs.Count; i++)
                {
                    var c = cs[i]; string p = $"{path}[{i}]";
                    var m = new QCondition { CompareOperator = (int)c.CompareOperator, Flags = (int)c.Flags, Unknown1 = Hex(c.Unknown1), Unknown2 = c.Unknown2 };
                    switch (c)
                    {
                        case IConditionFloatGetter f: m.Kind = "float"; m.Value = f.ComparisonValue;
                            Guard(c, typeof(IConditionFloatGetter), p, "ComparisonValue", "Data", "CompareOperator", "Flags", "Unknown1", "Unknown2"); break;
                        case IConditionGlobalGetter g: m.Kind = "global"; m.Global = g.ComparisonValue.FormKey.ToString();
                            Guard(c, typeof(IConditionGlobalGetter), p, "ComparisonValue", "Data", "CompareOperator", "Flags", "Unknown1", "Unknown2"); break;
                        default: Refusals.Add($"{p}: a {c.GetType().Name}"); break;
                    }
                    var d = c.Data;
                    m.Function = d.GetType().Name.Replace("BinaryOverlay", "").Replace("ConditionData", "");
                    m.RunOnType = (int)d.RunOnType; m.Reference = Fk(d.Reference); m.Unknown3 = d.Unknown3;
                    m.UseAliases = d.UseAliases; m.UsePackageData = d.UsePackageData;
                    if (!ConditionFunctions.TryGetValue(m.Function, out var fn))
                        Refusals.Add($"{p}: condition function {m.Function} is not in the writer's table");
                    else
                    {
                        fn.read(d, m);
                        Guard(d, fn.getter, p + ".Data", "Function", "RunOnType", "Reference", "Unknown3", "UseAliases", "UsePackageData",
                              "FirstParameter", "FirstUnusedStringParameter", "SecondParameter", "SecondUnusedStringParameter");
                    }
                    outp.Add(m);
                }
                return outp;
            }

            // ------------------------------------------------------------ VMAD
            private QVmad Vmad(IQuestAdapterGetter v, string path)
            {
                var m = new QVmad
                {
                    Version = v.Version, ObjectFormat = v.ObjectFormat, ExtraBindDataVersion = v.ExtraBindDataVersion,
                    Versioning = (int)v.Versioning,
                    FragmentScript = v.Script is { } s ? Script(s, path + ".Script") : null,
                    Fragments = v.Fragments.Select(f => new QFragment
                    {
                        Stage = f.Stage, Unknown = f.Unknown, StageIndex = f.StageIndex, Unknown2 = f.Unknown2,
                        ScriptName = f.ScriptName, FragmentName = f.FragmentName,
                    }).ToList(),
                };
                for (int i = 0; i < v.Fragments.Count; i++)
                    Guard(v.Fragments[i], typeof(IQuestScriptFragmentGetter), $"{path}.Fragments[{i}]", "Stage", "Unknown", "StageIndex", "Unknown2", "ScriptName", "FragmentName");
                for (int i = 0; i < v.Scripts.Count; i++) m.Scripts.Add(Script(v.Scripts[i], $"{path}.Scripts[{i}]"));
                for (int i = 0; i < v.Aliases.Count; i++)
                {
                    var a = v.Aliases[i]; string p = $"{path}.Aliases[{i}]";
                    var qa = new QAliasScripts { Version = a.Version, ObjectFormat = a.ObjectFormat, Property = Property(a.Property, p + ".Property") };
                    for (int k = 0; k < a.Scripts.Count; k++) qa.Scripts.Add(Script(a.Scripts[k], $"{p}.Scripts[{k}]"));
                    Guard(a, typeof(IQuestFragmentAliasGetter), p, "Version", "ObjectFormat", "Property", "Scripts");
                    m.Aliases.Add(qa);
                }
                Guard(v, typeof(IQuestAdapterGetter), path, "Version", "ObjectFormat", "ExtraBindDataVersion", "Versioning", "Script", "Fragments", "Scripts", "Aliases");
                return m;
            }

            private QScript Script(IScriptEntryGetter s, string path)
            {
                var m = new QScript { Name = s.Name, Flags = (int)s.Flags };
                for (int i = 0; i < s.Properties.Count; i++) m.Properties.Add(Property(s.Properties[i], $"{path}.Properties[{i}]"));
                Guard(s, typeof(IScriptEntryGetter), path, "Name", "Flags", "Properties");
                return m;
            }

            private QProperty Property(IScriptPropertyGetter p, string path)
            {
                var m = new QProperty { Name = p.Name, Flags = (int)p.Flags };
                QObjectRef Obj(IScriptObjectPropertyGetter o) => new() { Form = Fk(o.Object), Alias = o.Alias, Unused = o.Unused };
                switch (p)
                {
                    case IScriptIntPropertyGetter x: m.Type = "Int"; m.Int = x.Data; Guard(p, typeof(IScriptIntPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptFloatPropertyGetter x: m.Type = "Float"; m.Float = x.Data; Guard(p, typeof(IScriptFloatPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptBoolPropertyGetter x: m.Type = "Bool"; m.Bool = x.Data; Guard(p, typeof(IScriptBoolPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptStringPropertyGetter x: m.Type = "String"; m.String = x.Data; Guard(p, typeof(IScriptStringPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptObjectPropertyGetter x: m.Type = "Object"; m.Object = Obj(x); Guard(p, typeof(IScriptObjectPropertyGetter), path, "Name", "Flags", "Object", "Alias", "Unused"); break;
                    case IScriptIntListPropertyGetter x: m.Type = "IntList"; m.IntList = x.Data.ToList(); Guard(p, typeof(IScriptIntListPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptFloatListPropertyGetter x: m.Type = "FloatList"; m.FloatList = x.Data.ToList(); Guard(p, typeof(IScriptFloatListPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptBoolListPropertyGetter x: m.Type = "BoolList"; m.BoolList = x.Data.ToList(); Guard(p, typeof(IScriptBoolListPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptStringListPropertyGetter x: m.Type = "StringList"; m.StringList = x.Data.ToList(); Guard(p, typeof(IScriptStringListPropertyGetter), path, "Name", "Flags", "Data"); break;
                    case IScriptObjectListPropertyGetter x: m.Type = "ObjectList"; m.ObjectList = x.Objects.Select(Obj).ToList(); Guard(p, typeof(IScriptObjectListPropertyGetter), path, "Name", "Flags", "Objects"); break;
                    case IScriptStructListPropertyGetter x:
                        m.Type = "StructList";
                        m.StructList = new();
                        for (int i = 0; i < x.Structs.Count; i++)
                        {
                            var st = x.Structs[i];
                            m.StructList.Add(st.Members.Select((mp, j) => Property(mp, $"{path}.Structs[{i}].Members[{j}]")).ToList());
                            Guard(st, typeof(IScriptEntryStructsGetter), $"{path}.Structs[{i}]", "Members");
                        }
                        Guard(p, typeof(IScriptStructListPropertyGetter), path, "Name", "Flags", "Structs");
                        break;
                    default: Refusals.Add($"{path}: a {p.GetType().Name} ('{p.Name}'), a property type the model does not hold"); break;
                }
                return m;
            }
        }
    }
}
