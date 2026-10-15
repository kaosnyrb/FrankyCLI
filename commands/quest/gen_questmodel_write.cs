using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    /// <summary>
    /// WRITE A QUST FROM THE MODEL, EVERY FIELD BY NAME. ⛔ No DeepCopy, no Duplicate, no clone of any record,
    /// anywhere on this path: every object is `new`, and a field the model does not set is a field the record
    /// does not have. That is the whole point, and it is why the reader refuses rather than drops.
    /// </summary>
    public static partial class gen_questmodel
    {
        private static FormKey Key(string s) => s == FormKey.Null.ToString() ? FormKey.Null : FormKey.Factory(s);
        private static byte[] Bytes(string hex) => Convert.FromHexString(hex);
        private static T En<T>(int v) where T : struct, Enum => (T)Enum.ToObject(typeof(T), v);
        private static T En<T>(uint v) where T : struct, Enum => (T)Enum.ToObject(typeof(T), v);

        private static void Link<T>(IFormLinkNullable<T> l, string? fk) where T : class, IMajorRecordGetter
        { if (fk != null) l.SetTo((FormKey?)Key(fk)); }   // FormKey.Null here writes present-and-zero

        // ---------------------------------------------------------------- the quest
        private static Quest WriteQuest(QModel m)
        {
            var q = new Quest(Key(m.FormKey), StarfieldRelease.Starfield)
            {
                EditorID = m.EditorID,
                MajorRecordFlagsRaw = m.Header.MajorFlags,
                FormVersion = m.Header.FormVersion,
                Version2 = m.Header.Version2,
                VersionControl = m.Header.VersionControl,
                DialogConditions = m.DialogConditions == null ? null : new ExtendedList<Condition>(m.DialogConditions.Select(WriteCondition)),   // init-only
                Data = new QuestData
                {
                    Flags = En<Quest.Flag>(m.Data.Flags),
                    Priority = m.Data.Priority,
                    Type = En<Quest.TypeEnum>(m.Data.Type),
                    Unused = Bytes(m.Data.Unused),
                },
            };
            if (m.Event is int ev) q.Event = new RecordType(ev);
            q.Filter = m.Filter;
            q.Summary = m.Summary;
            q.SwfFile = m.SwfFile;
            q.ScriptComment = m.ScriptComment;
            q.Timestamp = m.Timestamp;
            q.Unknown = m.Unknown;
            if (m.Name != null) q.Name = m.Name;
            if (m.MissionBoardDescription != null) q.MissionBoardDescription = m.MissionBoardDescription;
            if (m.UnusedConditions != null) q.UnusedConditions = new ExtendedList<Condition>(m.UnusedConditions.Select(WriteCondition));
            if (m.MissionBoardInfoPanels != null)
                foreach (var p in m.MissionBoardInfoPanels)
                {
                    var panel = new QuestMissionBoardPanel();
                    if (p.Header != null) panel.Header = p.Header;
                    if (p.Line1 != null) panel.Line1 = p.Line1;
                    if (p.Line2 != null) panel.Line2 = p.Line2;
                    q.MissionBoardInfoPanels.Add(panel);
                }
            Link(q.QuestType, m.QuestType); Link(q.QuestFaction, m.QuestFaction); Link(q.QuestGroup, m.QuestGroup);
            Link(q.Location, m.Location); Link(q.QuestTimeLimit, m.QuestTimeLimit); Link(q.SourceQuest, m.SourceQuest);
            Link(q.MissionTypeKeyword, m.MissionTypeKeyword);
            if (m.Keywords != null) q.Keywords = new ExtendedList<IFormLinkGetter<IKeywordGetter>>(m.Keywords.Select(k => (IFormLinkGetter<IKeywordGetter>)new FormLink<IKeywordGetter>(Key(k))));
            q.TextDisplayGlobals.AddRange(m.TextDisplayGlobals.Select(k => (IFormLinkGetter<IGlobalGetter>)new FormLink<IGlobalGetter>(Key(k))));
            q.Stages.AddRange(m.Stages.Select(WriteStage));
            q.Objectives.AddRange(m.Objectives.Select(WriteObjective));
            q.Aliases = new ExtendedList<AQuestAlias>(m.Aliases.Select(WriteAlias));   // null on a new Quest, unlike Stages (manual part 13)
            if (m.Vmad != null) q.VirtualMachineAdapter = WriteVmad(m.Vmad);
            if (m.Children != null) WriteChildren(q, m.Children);
            return q;
        }

        private static QuestStage WriteStage(QStage s)
        {
            var st = new QuestStage { Index = s.Index, Flags = En<QuestStage.Flag>(s.Flags), Unknown = s.Unknown };
            foreach (var e in s.LogEntries)
            {
                var le = new QuestLogEntry
                {
                    Flags = e.Flags is int f ? En<QuestLogEntry.Flag>(f) : null,
                    Note = e.Note,
                    ScriptFlagComment = e.ScriptFlagComment,
                };
                if (e.Entry != null) le.Entry = e.Entry;
                le.Conditions.AddRange(e.Conditions.Select(WriteCondition));
                foreach (var sc in e.StageCompleteDatas)
                {
                    var d = new QuestStageCompleteData();
                    if (sc.QSRD != null) d.QSRD = Bytes(sc.QSRD);
                    Link(d.AffinityChange, sc.AffinityChange);
                    foreach (var r in sc.RewardDatas)
                    {
                        var rd = new QuestStageRewardData();
                        Link(rd.XpAwarded, r.XpAwarded); Link(rd.BonusCredits, r.BonusCredits);
                        rd.Rewards.AddRange(r.Rewards.Select(x => new QuestStageReward { Item = new FormLink<IConstructibleObjectTargetGetter>(Key(x.Item)), Count = x.Count }));
                        rd.Conditions.AddRange(r.Conditions.Select(WriteCondition));
                        d.RewardDatas.Add(rd);
                    }
                    le.StageCompleteDatas.Add(d);
                }
                st.LogEntries.Add(le);
            }
            return st;
        }

        private static QuestObjective WriteObjective(QObjective o)
        {
            var ob = new QuestObjective { Index = o.Index, Flags = o.Flags is int f ? En<QuestObjective.Flag>(f) : null };
            if (o.DisplayText != null) ob.DisplayText = o.DisplayText;
            foreach (var t in o.Targets)
            {
                var tg = new QuestObjectiveTarget
                {
                    AliasID = t.AliasID,
                    Flags = En<Quest.TargetFlag>(t.Flags),
                    QSTADataTypeState = En<QuestObjectiveTarget.QSTADataType>(t.DataTypeState),
                };
                if (t.Keyword != null) tg.Keyword.SetTo(Key(t.Keyword));
                tg.Conditions.AddRange(t.Conditions.Select(WriteCondition));
                ob.Targets.Add(tg);
            }
            return ob;
        }

        private static AQuestAlias WriteAlias(QAlias a)
        {
            if (a.Kind == "ref")
            {
                var r = new QuestReferenceAlias
                {
                    ID = a.ID, Name = a.Name, Flags = a.Flags is uint f ? En<QuestReferenceAlias.Flag>(f) : null,
                    ALFG = a.ALFG is long g ? (uint)g : null,
                    LegendaryRank = a.LegendaryRank, AliasIDToForceIntoWhenFilled = a.AliasIDToForceIntoWhenFilled,
                    ClosestToAlias = a.ClosestToAlias, ReferenceCollectionAliasID = a.ReferenceCollectionAliasID,
                    Conditions = a.Conditions == null ? null : new ExtendedList<Condition>(a.Conditions.Select(WriteCondition)),
                    Keywords = a.Keywords == null ? null : new ExtendedList<IFormLinkGetter<IKeywordGetter>>(a.Keywords.Select(k => (IFormLinkGetter<IKeywordGetter>)new FormLink<IKeywordGetter>(Key(k)))),
                };
                if (a.PackageData != null) r.PackageData.AddRange(a.PackageData.Select(k => (IFormLinkGetter<IPackageGetter>)new FormLink<IPackageGetter>(Key(k))));
                Link(r.ForcedReference, a.ForcedReference); Link(r.UniqueActor, a.UniqueActor); Link(r.UniqueBaseForm, a.UniqueBaseForm);
                Link(r.DisplayName, a.DisplayName); Link(r.DeathItem, a.DeathItem); Link(r.ForcedVoice, a.ForcedVoice); Link(r.VoiceTypes, a.VoiceTypes);
                if (a.InLocation is { } l)
                {
                    var lr = new LocationAliasReference { AliasID = l.AliasID };
                    Link(lr.Keyword, l.Keyword); Link(lr.RefType, l.RefType);
                    r.Location = lr;
                }
                if (a.Create is { } c)
                    r.CreateReferenceToObject = new CreateReferenceToObject
                    {
                        Object = new FormLink<IStarfieldMajorRecordGetter>(Key(c.Object)), AliasID = c.AliasID,
                        Create = En<CreateReferenceToObject.CreateEnum>(c.Create), Level = En<Level>(c.Level),
                    };
                if (a.External is { } x)
                {
                    var ex = new ExternalAliasReference { AliasID = x.AliasID };
                    Link(ex.Quest, x.Quest);
                    r.External = ex;
                }
                return r;
            }
            if (a.Kind == "loc")
            {
                var l = new QuestLocationAlias
                {
                    ID = a.ID, Name = a.Name, Flags = a.Flags is uint f ? En<QuestLocationAlias.Flag>(f) : null,
                    ALFG = a.ALFG is long g ? (int)g : null,
                    AliasIDToForceIntoWhenFilled = a.AliasIDToForceIntoWhenFilled, ClosestToAlias = a.ClosestToAlias,
                    SystemLocationAliasID = a.SystemLocationAliasID, ParentSystemLocationAliasID = a.ParentSystemLocationAliasID,
                    Conditions = a.Conditions == null ? null : new ExtendedList<Condition>(a.Conditions.Select(WriteCondition)),
                };
                Link(l.SpecificLocation, a.SpecificLocation); Link(l.LocationTypeKeyword, a.LocationTypeKeyword); Link(l.DisplayName, a.DisplayName);
                if (a.Alps is { } alps)
                {
                    var ap = new QuestLocationAliasALPS
                    {
                        Conditions = alps.Conditions == null ? null : new ExtendedList<Condition>(alps.Conditions.Select(WriteCondition)),
                    };
                    Link(ap.PcmTypeKeyword, alps.PcmTypeKeyword);
                    l.ALPS = ap;
                }
                return l;
            }
            if (a.Kind == "coll")
            {
                var c = new QuestCollectionAlias();
                foreach (var mem in a.Collection!)
                    c.Collection.Add(new CollectionAlias
                    {
                        ID = mem.ID, MaxInitialFillCount = mem.MaxInitialFillCount, ALAM = mem.ALAM,
                        ReferenceAlias = mem.ReferenceAlias == null ? null! : (QuestReferenceAlias)WriteAlias(mem.ReferenceAlias),
                    });
                return c;
            }
            throw new InvalidOperationException($"alias {a.ID}: kind '{a.Kind}' cannot be written");
        }

        // ---------------------------------------------------------------- conditions
        private static Condition WriteCondition(QCondition m)
        {
            if (!ConditionFunctions.TryGetValue(m.Function, out var fn))
                throw new InvalidOperationException($"condition function {m.Function} is not in the table");
            var d = fn.make();
            d.RunOnType = En<Condition.RunOnType>(m.RunOnType);
            if (m.Reference != null) d.Reference = new FormLink<IStarfieldMajorRecordGetter>(Key(m.Reference));
            d.Unknown3 = m.Unknown3;
            d.UseAliases = m.UseAliases;
            d.UsePackageData = m.UsePackageData;
            fn.write(d, m);
            Condition c = m.Kind switch
            {
                "float" => new ConditionFloat { ComparisonValue = m.Value ?? 0f, Data = d },
                "global" => new ConditionGlobal { ComparisonValue = new FormLink<IGlobalGetter>(Key(m.Global!)), Data = d },
                _ => throw new InvalidOperationException($"condition kind '{m.Kind}'"),
            };
            c.CompareOperator = En<CompareOperator>(m.CompareOperator);
            c.Flags = En<Condition.Flag>(m.Flags);
            c.Unknown1 = Bytes(m.Unknown1);
            c.Unknown2 = m.Unknown2;
            return c;
        }

        private sealed record Fn(Type getter, Func<ConditionData> make, Action<IConditionDataGetter, QCondition> read, Action<ConditionData, QCondition> write);

        private static void ReadFirst<T>(IFormLinkOrIndexGetter<T> p, QCondition m) where T : class, IMajorRecordGetter
        {
            if (m.UseAliases || m.UsePackageData) m.FirstIndex = p.Index;
            else m.FirstLink = p.Link.IsNull ? null : p.Link.FormKey.ToString();
        }

        private static void WriteFirst<T>(IFormLinkOrIndex<T> p, QCondition m) where T : class, IMajorRecordGetter
        {
            if (m.UseAliases || m.UsePackageData) p.Index = m.FirstIndex;
            else if (m.FirstLink != null) p.Link.SetTo(Key(m.FirstLink));
        }

        private static void ReadSecond<T>(IFormLinkOrIndexGetter<T> p, QCondition m) where T : class, IMajorRecordGetter
        {
            if (m.UseAliases || m.UsePackageData) m.SecondIndex = p.Index;
            else m.SecondLink = p.Link.IsNull ? null : p.Link.FormKey.ToString();
        }

        private static void WriteSecond<T>(IFormLinkOrIndex<T> p, QCondition m) where T : class, IMajorRecordGetter
        {
            if (m.UseAliases || m.UsePackageData) p.Index = m.SecondIndex;
            else if (m.SecondLink != null) p.Link.SetTo(Key(m.SecondLink));
        }

        /// ⭐ THE CLOSED SET. Adding a condition function is one row here; a function not here is refused on read.
        private static readonly Dictionary<string, Fn> ConditionFunctions = new()
        {
            ["GetDistanceGalacticParsec"] = new(typeof(IGetDistanceGalacticParsecConditionDataGetter), () => new GetDistanceGalacticParsecConditionData(),
                (g, m) => { var x = (IGetDistanceGalacticParsecConditionDataGetter)g; ReadFirst(x.FirstParameter, m); m.FirstUnusedString = x.FirstUnusedStringParameter; m.SecondParameter = x.SecondParameter; m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (GetDistanceGalacticParsecConditionData)d; WriteFirst(x.FirstParameter, m); x.FirstUnusedStringParameter = m.FirstUnusedString; x.SecondParameter = m.SecondParameter; x.SecondUnusedStringParameter = m.SecondUnusedString; }),
            ["GetIsID"] = new(typeof(IGetIsIDConditionDataGetter), () => new GetIsIDConditionData(),
                (g, m) => { var x = (IGetIsIDConditionDataGetter)g; ReadFirst(x.FirstParameter, m); m.FirstUnusedString = x.FirstUnusedStringParameter; m.SecondParameter = x.SecondParameter; m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (GetIsIDConditionData)d; WriteFirst(x.FirstParameter, m); x.FirstUnusedStringParameter = m.FirstUnusedString; x.SecondParameter = m.SecondParameter; x.SecondUnusedStringParameter = m.SecondUnusedString; }),
            ["IsTrueForConditionForm"] =new(typeof(IIsTrueForConditionFormConditionDataGetter), () => new IsTrueForConditionFormConditionData(),
                (g, m) => { var x = (IIsTrueForConditionFormConditionDataGetter)g; ReadFirst(x.FirstParameter, m); m.FirstUnusedString = x.FirstUnusedStringParameter; m.SecondParameter = x.SecondParameter; m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (IsTrueForConditionFormConditionData)d; WriteFirst(x.FirstParameter, m); x.FirstUnusedStringParameter = m.FirstUnusedString; x.SecondParameter = m.SecondParameter; x.SecondUnusedStringParameter = m.SecondUnusedString; }),
            // The odd shape: the FIRST parameter is a plain int (an alias number), the SECOND is the link-or-index.
            ["IsInSameCurrentLocationAsAlias"] = new(typeof(IIsInSameCurrentLocationAsAliasConditionDataGetter), () => new IsInSameCurrentLocationAsAliasConditionData(),
                (g, m) => { var x = (IIsInSameCurrentLocationAsAliasConditionDataGetter)g; m.FirstInt = x.FirstParameter; m.FirstUnusedString = x.FirstUnusedStringParameter; ReadSecond(x.SecondParameter, m); m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (IsInSameCurrentLocationAsAliasConditionData)d; x.FirstParameter = m.FirstInt ?? 0; x.FirstUnusedStringParameter = m.FirstUnusedString; WriteSecond(x.SecondParameter, m); x.SecondUnusedStringParameter = m.SecondUnusedString; }),
            ["LocationHasRefType"] = new(typeof(ILocationHasRefTypeConditionDataGetter), () => new LocationHasRefTypeConditionData(),
                (g, m) => { var x = (ILocationHasRefTypeConditionDataGetter)g; ReadFirst(x.FirstParameter, m); m.FirstUnusedString = x.FirstUnusedStringParameter; m.SecondParameter = x.SecondParameter; m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (LocationHasRefTypeConditionData)d; WriteFirst(x.FirstParameter, m); x.FirstUnusedStringParameter = m.FirstUnusedString; x.SecondParameter = m.SecondParameter; x.SecondUnusedStringParameter = m.SecondUnusedString; }),
            ["LocationHasKeyword"] = new(typeof(ILocationHasKeywordConditionDataGetter), () => new LocationHasKeywordConditionData(),
                (g, m) => { var x = (ILocationHasKeywordConditionDataGetter)g; ReadFirst(x.FirstParameter, m); m.FirstUnusedString = x.FirstUnusedStringParameter; m.SecondParameter = x.SecondParameter; m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (LocationHasKeywordConditionData)d; WriteFirst(x.FirstParameter, m); x.FirstUnusedStringParameter = m.FirstUnusedString; x.SecondParameter = m.SecondParameter; x.SecondUnusedStringParameter = m.SecondUnusedString; }),
            ["GetDistance"] = new(typeof(IGetDistanceConditionDataGetter), () => new GetDistanceConditionData(),
                (g, m) => { var x = (IGetDistanceConditionDataGetter)g; ReadFirst(x.FirstParameter, m); m.FirstUnusedString = x.FirstUnusedStringParameter; m.SecondParameter = x.SecondParameter; m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (GetDistanceConditionData)d; WriteFirst(x.FirstParameter, m); x.FirstUnusedStringParameter = m.FirstUnusedString; x.SecondParameter = m.SecondParameter; x.SecondUnusedStringParameter = m.SecondUnusedString; }),
            ["GetStageDone"] = new(typeof(IGetStageDoneConditionDataGetter), () => new GetStageDoneConditionData(),
                (g, m) => { var x = (IGetStageDoneConditionDataGetter)g; ReadFirst(x.FirstParameter, m); m.FirstUnusedString = x.FirstUnusedStringParameter; m.SecondParameter = x.SecondParameter; m.SecondUnusedString = x.SecondUnusedStringParameter; },
                (d, m) => { var x = (GetStageDoneConditionData)d; WriteFirst(x.FirstParameter, m); x.FirstUnusedStringParameter = m.FirstUnusedString; x.SecondParameter = m.SecondParameter; x.SecondUnusedStringParameter = m.SecondUnusedString; }),
        };

        // ---------------------------------------------------------------- VMAD
        private static QuestAdapter WriteVmad(QVmad v)
        {
            var a = new QuestAdapter
            {
                Version = v.Version, ObjectFormat = v.ObjectFormat, ExtraBindDataVersion = v.ExtraBindDataVersion,
                Versioning = En<QuestAdapter.VersioningBreaks>(v.Versioning),
                Script = v.FragmentScript == null ? null! : WriteScript(v.FragmentScript),
            };
            a.Fragments.AddRange(v.Fragments.Select(f => new QuestScriptFragment
            {
                Stage = f.Stage, Unknown = f.Unknown, StageIndex = f.StageIndex, Unknown2 = f.Unknown2,
                ScriptName = f.ScriptName!, FragmentName = f.FragmentName!,
            }));
            a.Scripts.AddRange(v.Scripts.Select(WriteScript));
            foreach (var al in v.Aliases)
            {
                var fa = new QuestFragmentAlias { Version = al.Version, ObjectFormat = al.ObjectFormat, Property = (ScriptObjectProperty)WriteProperty(al.Property) };
                fa.Scripts.AddRange(al.Scripts.Select(WriteScript));
                a.Aliases.Add(fa);
            }
            return a;
        }

        private static ScriptEntry WriteScript(QScript s)
        {
            var e = new ScriptEntry { Name = s.Name, Flags = En<ScriptEntry.Flag>(s.Flags) };
            e.Properties.AddRange(s.Properties.Select(WriteProperty));
            return e;
        }

        private static ScriptObjectProperty Obj(QObjectRef o)
        {
            var p = new ScriptObjectProperty { Alias = o.Alias, Unused = o.Unused };
            if (o.Form != null) p.Object = new FormLink<IStarfieldMajorRecordGetter>(Key(o.Form));
            return p;
        }

        private static ScriptProperty WriteProperty(QProperty m)
        {
            ScriptProperty p = m.Type switch
            {
                "Int" => new ScriptIntProperty { Data = m.Int ?? 0 },
                "Float" => new ScriptFloatProperty { Data = m.Float ?? 0f },
                "Bool" => new ScriptBoolProperty { Data = m.Bool ?? false },
                "String" => new ScriptStringProperty { Data = m.String ?? "" },
                "Object" => Obj(m.Object!),
                "IntList" => new ScriptIntListProperty { Data = new ExtendedList<int>(m.IntList!) },
                "FloatList" => new ScriptFloatListProperty { Data = new ExtendedList<float>(m.FloatList!) },
                "BoolList" => new ScriptBoolListProperty { Data = new ExtendedList<bool>(m.BoolList!) },
                "StringList" => new ScriptStringListProperty { Data = new ExtendedList<string>(m.StringList!) },
                "ObjectList" => new ScriptObjectListProperty { Objects = new ExtendedList<ScriptObjectProperty>(m.ObjectList!.Select(Obj)) },
                _ => throw new InvalidOperationException($"property type '{m.Type}'"),
            };
            p.Name = m.Name;
            p.Flags = En<ScriptProperty.Flag>(m.Flags);
            return p;
        }
    }
}
