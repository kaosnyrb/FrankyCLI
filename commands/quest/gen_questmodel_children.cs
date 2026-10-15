using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FrankyCLI
{
    // ==================================================================== the model
    /// A quest's CHILD records: its scenes and its dialogue topics (each topic holding its INFO records). They are
    /// records of their own with their own FormKeys, written in the quest's group, so they carry their own header.
    public sealed class QChildren
    {
        public List<QScene> Scenes { get; set; } = new();
        public List<QTopic> Topics { get; set; } = new();
    }

    public sealed class QScene
    {
        public string FormKey { get; set; } = "";
        public QHeader Header { get; set; } = new();
        public string? EditorID { get; set; }
        public int? Flags { get; set; }
        public string? Quest { get; set; }
        public uint? LastActionIndex { get; set; }
        public uint? Index { get; set; }
        public string? Notes { get; set; }
        public string? VNAM { get; set; }
        public string? BOLV { get; set; }
        public string? SCPI { get; set; }
        public string? JNAM { get; set; }
        public string? SPRK { get; set; }
        public string? SPRW { get; set; }
        public bool SCSP { get; set; }
        public bool SPRP { get; set; }
        public bool SPDF { get; set; }
        public bool SPPQ { get; set; }
        public bool DEVT { get; set; }
        public List<QCondition>? Conditions { get; set; }
        public List<QPhase> Phases { get; set; } = new();
        public List<QSceneActor> Actors { get; set; } = new();
        /// Absent on a scene with no actions (vanilla has them), so null and empty stay different.
        public List<QSceneAction>? Actions { get; set; }
    }

    public sealed class QPhase
    {
        public string? Name { get; set; }
        public uint? EditorWidth { get; set; }
        public int? Flags { get; set; }
        public string? SPMV { get; set; }
        public List<QCondition>? StartConditions { get; set; }
        public List<QCondition>? CompletionConditions { get; set; }
    }

    public sealed class QSceneActor { public uint ID { get; set; } public int Flags { get; set; } public int BehaviorFlags { get; set; } }

    /// One scene action. Kind is the Mutagen class less "SceneAction"; only "Radio" is held so far.
    public sealed class QSceneAction
    {
        public string Kind { get; set; } = "";
        public string? Name { get; set; }
        public string? SceneNotes { get; set; }
        public int? AliasID { get; set; }
        public uint? Index { get; set; }
        public int? Flags { get; set; }
        public uint StartPhase { get; set; }
        public uint EndPhase { get; set; }
        // Radio and Dialogue
        public string? Topic { get; set; }
        public string? DialogueSubtype { get; set; }
        // Dialogue
        public float? LoopingMax { get; set; }
        public float? LoopingMin { get; set; }
        /// HNAM. Present-and-empty on every Delve talk action, so absent and empty are different records.
        public QHeadTracking? HeadTracking { get; set; }
        public QSound? WED0 { get; set; }
    }

    public sealed class QHeadTracking { public List<int>? Aliases { get; set; } public bool ForceRotate { get; set; } public bool ForceRotateMustComplete { get; set; } }

    public sealed class QSound { public string Start { get; set; } = ""; public string Stop { get; set; } = ""; public string? Condition { get; set; } public string? EventMapping { get; set; } }

    public sealed class QTopic
    {
        public string FormKey { get; set; } = "";
        public QHeader Header { get; set; } = new();
        public string? EditorID { get; set; }
        public string? Name { get; set; }
        public float Priority { get; set; }
        public string? Branch { get; set; }
        public string? Quest { get; set; }
        public string? Keyword { get; set; }
        public string? AffinityEvent { get; set; }
        public int TopicFlags { get; set; }
        public int Category { get; set; }
        public int Subtype { get; set; }
        public int SubtypeName { get; set; }
        public int Timestamp { get; set; }
        public int Unknown { get; set; }
        public List<string> TopicInfoList { get; set; } = new();
        public List<QInfo> Infos { get; set; } = new();
    }

    /// An INFO record (Mutagen: DialogResponses): one spoken response group under a topic.
    public sealed class QInfo
    {
        public string FormKey { get; set; } = "";
        public QHeader Header { get; set; } = new();
        public string? EditorID { get; set; }
        public short? FlagsFlags { get; set; }
        public float? FlagsResetHours { get; set; }
        public string? TPIC { get; set; }
        public string? INTV { get; set; }
        public string? SharedDialog { get; set; }
        public string? DialogGroup { get; set; }
        public string? Prompt { get; set; }
        public string? Speaker { get; set; }
        public string? StartScene { get; set; }
        public string? StartScenePhase { get; set; }
        public string? ResetGlobal { get; set; }
        public int? SubtitlePriority { get; set; }
        public bool COCT { get; set; }
        public string? AffinityEvent { get; set; }
        public string? SpeechChallenge { get; set; }
        public string? Perk { get; set; }
        public short? SetStageOnBegin { get; set; }
        public short? SetStageOnEnd { get; set; }
        public List<QCondition>? Conditions { get; set; }
        public List<QLine> Lines { get; set; } = new();
    }

    /// One line of a response (Mutagen: DialogResponse): the text, its .wem number and the text hash.
    public sealed class QLine
    {
        public string? Emotion { get; set; }
        public uint WEMFile { get; set; }
        public float EmotionOut { get; set; }
        public string? ResponseText { get; set; }
        public string? ScriptNotes { get; set; }
        public string? Edits { get; set; }
        public string? AlternateLipText { get; set; }
        public string? TextHash { get; set; }
    }

    public static partial class gen_questmodel
    {
        // ==================================================================== read
        private sealed partial class Reader
        {
            private static QHeader Head(IStarfieldMajorRecordGetter r) => new() { MajorFlags = r.MajorRecordFlagsRaw, FormVersion = r.FormVersion, Version2 = r.Version2, VersionControl = r.VersionControl };
            private static string? Hx(ReadOnlyMemorySlice<byte>? m) => m is { } x ? Convert.ToHexString(x.ToArray()) : null;

            public QChildren ReadChildren(IQuestGetter q)
            {
                var c = new QChildren();
                for (int i = 0; i < q.Scenes.Count; i++) c.Scenes.Add(Scene(q.Scenes[i], $"Scenes[{i}]"));
                for (int i = 0; i < q.DialogTopics.Count; i++) c.Topics.Add(Topic(q.DialogTopics[i], $"DialogTopics[{i}]"));
                if (q.DialogBranches.Count > 0) Refusals.Add($"DialogBranches: {q.DialogBranches.Count} branch record(s), a child the model does not hold yet");
                return c;
            }

            private QScene Scene(ISceneGetter s, string path)
            {
                var m = new QScene
                {
                    FormKey = s.FormKey.ToString(), Header = Head(s), EditorID = s.EditorID, Flags = s.Flags is { } f ? (int)f : null,
                    Quest = Fk(s.Quest), LastActionIndex = s.LastActionIndex, Index = s.Index, Notes = s.Notes,
                    VNAM = Hx(s.VNAM), BOLV = Hx(s.BOLV), SCPI = Hx(s.SCPI), JNAM = Hx(s.JNAM), SPRK = Hx(s.SPRK), SPRW = Hx(s.SPRW),
                    SCSP = s.SCSP, SPRP = s.SPRP, SPDF = s.SPDF, SPPQ = s.SPPQ, DEVT = s.DEVT,
                    Conditions = s.Conditions == null ? null : Conditions(s.Conditions, path + ".Conditions"),
                };
                for (int i = 0; i < s.Phases.Count; i++)
                {
                    var p = s.Phases[i]; string pp = $"{path}.Phases[{i}]";
                    m.Phases.Add(new QPhase
                    {
                        Name = p.Name, EditorWidth = p.EditorWidth, Flags = p.Flags is { } pf ? (int)pf : null, SPMV = Hx(p.SPMV),
                        StartConditions = p.StartConditions == null ? null : Conditions(p.StartConditions, pp + ".StartConditions"),
                        CompletionConditions = p.CompletionConditions == null ? null : Conditions(p.CompletionConditions, pp + ".CompletionConditions"),
                    });
                    Guard(p, typeof(IScenePhaseGetter), pp, "Name", "EditorWidth", "Flags", "SPMV", "StartConditions", "CompletionConditions");
                }
                for (int i = 0; i < s.Actors.Count; i++)
                {
                    var x = s.Actors[i];
                    m.Actors.Add(new QSceneActor { ID = x.ID, Flags = (int)x.Flags, BehaviorFlags = (int)x.BehaviorFlags });
                    Guard(x, typeof(ISceneActorGetter), $"{path}.Actors[{i}]", "ID", "Flags", "BehaviorFlags");
                }
                if (s.Actions != null) m.Actions = new();
                for (int i = 0; i < (s.Actions?.Count ?? 0); i++)
                {
                    var x = s.Actions![i]; string ap = $"{path}.Actions[{i}]";
                    var qa = new QSceneAction
                    {
                        Name = x.Name, SceneNotes = x.SceneNotes, AliasID = x.AliasID, Index = x.Index,
                        Flags = x.Flags is { } af ? (int)af : null, StartPhase = x.StartPhase, EndPhase = x.EndPhase,
                    };
                    if (x is IRadioSceneActionGetter r)
                    {
                        qa.Kind = "Radio"; qa.Topic = r.Topic.FormKey.ToString(); qa.DialogueSubtype = Fk(r.DialogueSubtype);
                        Guard(r, typeof(IRadioSceneActionGetter), ap, "Name", "SceneNotes", "AliasID", "Index", "Flags", "StartPhase", "EndPhase", "Topic", "DialogueSubtype");
                    }
                    else if (x is IDialogueSceneActionGetter d)
                    {
                        qa.Kind = "Dialogue"; qa.Topic = Fk(d.Topic); qa.DialogueSubtype = Fk(d.DialogueSubtype);
                        qa.LoopingMax = d.LoopingMax; qa.LoopingMin = d.LoopingMin;
                        if (d.HNAM is { } h)
                        {
                            qa.HeadTracking = new QHeadTracking { Aliases = h.Aliases?.ToList(), ForceRotate = h.ForceRotate, ForceRotateMustComplete = h.ForceRotateMustComplete };
                            Guard(h, typeof(IHeadTrackingGetter), ap + ".HNAM", "Aliases", "ForceRotate", "ForceRotateMustComplete");
                        }
                        if (d.WED0 is { } w)
                        {
                            qa.WED0 = new QSound { Start = w.Start.ToString(), Stop = w.Stop.ToString(), Condition = Fk(w.Condition), EventMapping = Fk(w.EventMapping) };
                            Guard(w, typeof(ISoundReferenceGetter), ap + ".WED0", "Start", "Stop", "Condition", "EventMapping");
                        }
                        Guard(d, typeof(IDialogueSceneActionGetter), ap, "Name", "SceneNotes", "AliasID", "Index", "Flags", "StartPhase", "EndPhase",
                              "Topic", "DialogueSubtype", "LoopingMax", "LoopingMin", "HNAM", "WED0");
                    }
                    else Refusals.Add($"{ap}: a {x.GetType().Name.Replace("BinaryOverlay", "")}, a scene action the model does not hold yet");
                    m.Actions!.Add(qa);
                }
                Guard(s, typeof(ISceneGetter), path, "Flags", "Quest", "LastActionIndex", "Index", "Notes", "VNAM", "BOLV", "SCPI", "JNAM",
                      "SPRK", "SPRW", "SCSP", "SPRP", "SPDF", "SPPQ", "DEVT", "Phases", "Actors", "Actions", "Conditions");
                return m;
            }

            private QTopic Topic(IDialogTopicGetter t, string path)
            {
                var m = new QTopic
                {
                    FormKey = t.FormKey.ToString(), Header = Head(t), EditorID = t.EditorID, Name = Str(t.Name), Priority = t.Priority,
                    Branch = Fk(t.Branch), Quest = Fk(t.Quest), Keyword = Fk(t.Keyword), AffinityEvent = Fk(t.AffinityEvent),
                    TopicFlags = (int)t.TopicFlags, Category = (int)t.Category, Subtype = (int)t.Subtype, SubtypeName = (int)t.SubtypeName,
                    Timestamp = t.Timestamp, Unknown = t.Unknown,
                    TopicInfoList = t.TopicInfoList.Select(x => x.FormKey.ToString()).ToList(),
                };
                for (int i = 0; i < t.Responses.Count; i++) m.Infos.Add(Info(t.Responses[i], $"{path}.Responses[{i}]"));
                Guard(t, typeof(IDialogTopicGetter), path, "Name", "Priority", "Branch", "Quest", "Keyword", "AffinityEvent", "TopicFlags",
                      "Category", "Subtype", "SubtypeName", "Timestamp", "Unknown", "TopicInfoList", "Responses");
                return m;
            }

            private QInfo Info(IDialogResponsesGetter r, string path)
            {
                var m = new QInfo
                {
                    FormKey = r.FormKey.ToString(), Header = Head(r), EditorID = r.EditorID,
                    FlagsFlags = r.Flags?.Flags, FlagsResetHours = r.Flags?.ResetHours,
                    TPIC = Hx(r.TPIC), INTV = Hx(r.INTV), SharedDialog = Fk(r.SharedDialog), DialogGroup = Fk(r.DialogGroup),
                    Prompt = Str(r.Prompt), Speaker = Fk(r.Speaker), StartScene = Fk(r.StartScene), StartScenePhase = r.StartScenePhase,
                    ResetGlobal = Fk(r.ResetGlobal), SubtitlePriority = r.SubtitlePriority is { } sp ? (int)sp : null, COCT = r.COCT,
                    AffinityEvent = Fk(r.AffinityEvent), SpeechChallenge = Fk(r.SpeechChallenge), Perk = Fk(r.Perk),
                    SetStageOnBegin = r.SetParentQuestStage?.OnBegin, SetStageOnEnd = r.SetParentQuestStage?.OnEnd,
                    Conditions = r.Conditions == null ? null : Conditions(r.Conditions, path + ".Conditions"),
                };
                if (r.Flags != null) Guard(r.Flags, typeof(IDialogResponseFlagsGetter), path + ".Flags", "Flags", "ResetHours");
                if (r.SetParentQuestStage != null) Guard(r.SetParentQuestStage, typeof(IDialogSetParentQuestStageGetter), path + ".SetParentQuestStage", "OnBegin", "OnEnd");
                for (int i = 0; i < r.Responses.Count; i++)
                {
                    var l = r.Responses[i]; string lp = $"{path}.Responses[{i}]";
                    m.Lines.Add(new QLine
                    {
                        Emotion = Fk(l.Emotion), WEMFile = l.WEMFile, EmotionOut = l.EmotionOut, ResponseText = Str(l.ResponseText),
                        ScriptNotes = l.ScriptNotes, Edits = l.Edits, AlternateLipText = l.AlternateLipText, TextHash = Hx(l.TextHash),
                    });
                    Guard(l, typeof(IDialogResponseGetter), lp, "Emotion", "WEMFile", "EmotionOut", "ResponseText", "ScriptNotes", "Edits", "AlternateLipText", "TextHash");
                }
                Guard(r, typeof(IDialogResponsesGetter), path, "Flags", "TPIC", "INTV", "SharedDialog", "DialogGroup", "Prompt", "Speaker", "StartScene",
                      "StartScenePhase", "ResetGlobal", "SubtitlePriority", "COCT", "AffinityEvent", "SpeechChallenge", "Perk", "SetParentQuestStage",
                      "Conditions", "Responses");
                return m;
            }
        }

        // ==================================================================== write
        private static void Stamp(StarfieldMajorRecord r, QHeader h, string? edid)
        {
            r.EditorID = edid; r.MajorRecordFlagsRaw = h.MajorFlags; r.FormVersion = h.FormVersion; r.Version2 = h.Version2; r.VersionControl = h.VersionControl;
        }

        private static void WriteChildren(Quest q, QChildren c)
        {
            foreach (var s in c.Scenes) q.Scenes.Add(WriteScene(s));
            foreach (var t in c.Topics) q.DialogTopics.Add(WriteTopic(t));
        }

        private static Scene WriteScene(QScene m)
        {
            var s = new Scene(Key(m.FormKey), StarfieldRelease.Starfield)
            {
                Flags = m.Flags is int f ? En<Scene.Flag>(f) : null, LastActionIndex = m.LastActionIndex, Index = m.Index, Notes = m.Notes,
                SCSP = m.SCSP, SPRP = m.SPRP, SPDF = m.SPDF, SPPQ = m.SPPQ, DEVT = m.DEVT,
                Conditions = m.Conditions == null ? null! : new ExtendedList<Condition>(m.Conditions.Select(WriteCondition)),
            };
            Stamp(s, m.Header, m.EditorID);
            Link(s.Quest, m.Quest);
            if (m.VNAM != null) s.VNAM = Bytes(m.VNAM);
            if (m.BOLV != null) s.BOLV = Bytes(m.BOLV);
            if (m.SCPI != null) s.SCPI = Bytes(m.SCPI);
            if (m.JNAM != null) s.JNAM = Bytes(m.JNAM);
            if (m.SPRK != null) s.SPRK = Bytes(m.SPRK);
            if (m.SPRW != null) s.SPRW = Bytes(m.SPRW);
            foreach (var p in m.Phases)
            {
                var ph = new ScenePhase
                {
                    Name = p.Name!, EditorWidth = p.EditorWidth, Flags = p.Flags is int pf ? En<ScenePhase.Flag>(pf) : null,
                    StartConditions = p.StartConditions == null ? null! : new ExtendedList<Condition>(p.StartConditions.Select(WriteCondition)),
                    CompletionConditions = p.CompletionConditions == null ? null! : new ExtendedList<Condition>(p.CompletionConditions.Select(WriteCondition)),
                };
                if (p.SPMV != null) ph.SPMV = Bytes(p.SPMV);
                s.Phases.Add(ph);
            }
            foreach (var x in m.Actors)
                s.Actors.Add(new SceneActor { ID = x.ID, Flags = En<SceneActor.Flag>(x.Flags), BehaviorFlags = En<SceneActor.BehaviorFlag>(x.BehaviorFlags) });
            if (m.Actions != null) s.Actions = new ExtendedList<ASceneAction>();   // null on a new Scene, unlike Phases and Actors
            foreach (var x in m.Actions ?? new())
            {
                if (x.Kind == "Dialogue")
                {
                    var d = new DialogueSceneAction
                    {
                        Name = x.Name, SceneNotes = x.SceneNotes, AliasID = x.AliasID, Index = x.Index,
                        Flags = x.Flags is int df ? En<ASceneAction.Flag>(df) : null, StartPhase = x.StartPhase, EndPhase = x.EndPhase,
                        LoopingMax = x.LoopingMax, LoopingMin = x.LoopingMin,
                    };
                    Link(d.Topic, x.Topic); Link(d.DialogueSubtype, x.DialogueSubtype);
                    if (x.HeadTracking is { } h)
                    {
                        var ht = new HeadTracking { ForceRotate = h.ForceRotate, ForceRotateMustComplete = h.ForceRotateMustComplete };
                        if (h.Aliases != null) ht.Aliases = new ExtendedList<int>(h.Aliases);
                        d.HNAM = ht;
                    }
                    if (x.WED0 is { } w)
                    {
                        var sr = new SoundReference { Start = Guid.Parse(w.Start), Stop = Guid.Parse(w.Stop) };
                        if (w.Condition != null) sr.Condition.SetTo(Key(w.Condition));
                        if (w.EventMapping != null) sr.EventMapping.SetTo(Key(w.EventMapping));
                        d.WED0 = sr;
                    }
                    s.Actions.Add(d);
                    continue;
                }
                if (x.Kind != "Radio") throw new InvalidOperationException($"scene action kind '{x.Kind}' cannot be written");
                var a = new RadioSceneAction
                {
                    Name = x.Name, SceneNotes = x.SceneNotes, AliasID = x.AliasID, Index = x.Index,
                    Flags = x.Flags is int af ? En<ASceneAction.Flag>(af) : null, StartPhase = x.StartPhase, EndPhase = x.EndPhase,
                    Topic = new FormLink<IDialogTopicGetter>(Key(x.Topic!)),
                };
                Link(a.DialogueSubtype, x.DialogueSubtype);
                s.Actions.Add(a);
            }
            return s;
        }

        private static DialogTopic WriteTopic(QTopic m)
        {
            var t = new DialogTopic(Key(m.FormKey), StarfieldRelease.Starfield)
            {
                Priority = m.Priority, TopicFlags = En<DialogTopic.TopicFlag>(m.TopicFlags), Category = En<DialogTopic.CategoryEnum>(m.Category),
                Subtype = En<DialogTopic.SubtypeEnum>(m.Subtype), SubtypeName = En<DialogTopic.SubtypeNameEnum>(m.SubtypeName),
                Timestamp = m.Timestamp, Unknown = m.Unknown,
            };
            Stamp(t, m.Header, m.EditorID);
            if (m.Name != null) t.Name = m.Name;
            Link(t.Branch, m.Branch); Link(t.Keyword, m.Keyword); Link(t.AffinityEvent, m.AffinityEvent);
            if (m.Quest != null) t.Quest.SetTo(Key(m.Quest));
            t.TopicInfoList ??= new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>();   // null on a new DialogTopic
            t.TopicInfoList.AddRange(m.TopicInfoList.Select(k => (IFormLinkGetter<IDialogResponsesGetter>)new FormLink<IDialogResponsesGetter>(Key(k))));
            foreach (var i in m.Infos) t.Responses.Add(WriteInfo(i));
            return t;
        }

        private static DialogResponses WriteInfo(QInfo m)
        {
            var r = new DialogResponses(Key(m.FormKey), StarfieldRelease.Starfield)
            {
                StartScenePhase = m.StartScenePhase, COCT = m.COCT,
                SubtitlePriority = m.SubtitlePriority is int sp ? En<DialogResponses.SubtitlePriorityLevel>(sp) : null,
                Conditions = m.Conditions == null ? null! : new ExtendedList<Condition>(m.Conditions.Select(WriteCondition)),
            };
            Stamp(r, m.Header, m.EditorID);
            if (m.FlagsFlags != null) r.Flags = new DialogResponseFlags { Flags = m.FlagsFlags.Value, ResetHours = m.FlagsResetHours ?? 0f };
            if (m.TPIC != null) r.TPIC = Bytes(m.TPIC);
            if (m.INTV != null) r.INTV = Bytes(m.INTV);
            if (m.Prompt != null) r.Prompt = m.Prompt;
            Link(r.SharedDialog, m.SharedDialog); Link(r.DialogGroup, m.DialogGroup); Link(r.Speaker, m.Speaker); Link(r.StartScene, m.StartScene);
            Link(r.ResetGlobal, m.ResetGlobal); Link(r.AffinityEvent, m.AffinityEvent); Link(r.SpeechChallenge, m.SpeechChallenge); Link(r.Perk, m.Perk);
            if (m.SetStageOnBegin != null) r.SetParentQuestStage = new DialogSetParentQuestStage { OnBegin = m.SetStageOnBegin.Value, OnEnd = m.SetStageOnEnd ?? 0 };
            foreach (var l in m.Lines)
            {
                var line = new DialogResponse { WEMFile = l.WEMFile, EmotionOut = l.EmotionOut, ScriptNotes = l.ScriptNotes!, Edits = l.Edits!, AlternateLipText = l.AlternateLipText! };
                if (l.Emotion != null) line.Emotion.SetTo(Key(l.Emotion));
                if (l.ResponseText != null) line.ResponseText = l.ResponseText;
                if (l.TextHash != null) line.TextHash = Bytes(l.TextHash);
                r.Responses.Add(line);
            }
            return r;
        }
    }
}
