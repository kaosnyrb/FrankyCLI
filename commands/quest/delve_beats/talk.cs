using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FrankyCLI
{
    public static partial class gen_delve
    {
        /// <summary>
        /// talk: a person who stands at the beat's marker from quest start, a greeting, and the questions the player can
        /// ask. exchanges[0] closes the conversation and sets the beat's stage. It has no object, no box and no radio line:
        /// the person is the object and his voice is the line (his "either or").
        ///
        /// Moved here from GradeTalk, Slots' talk branch and BuildTalk (split step (d4), 2026-10-15), unchanged. What stays
        /// where it was: the person's NPC, cloned in the people loop the endings share (Messages); HandBackTalk, a rule
        /// between beats (a talk that ends the Delve takes what the player carries, in the lint and the fragment); the
        /// person rules over the other types; the perk lookups in ResolveInputs; Grade's ring predicate.
        /// </summary>
        private sealed class TalkBeat : BeatKind
        {
            internal override string Name => "talk";

            // The talk beat's limits, docs/specs/npc_dialogue.md: a prompt is a menu line, a reply a spoken one.
            private const int TalkPromptMax = 60, TalkReplyMax = 200;
            /// <summary>person.hellos: one is picked each walk-up; MQ101's Lin carries 3, Heller 2.</summary>
            private const int TalkHellosMax = 4;

            /// <summary>The perk families vanilla tags a dialogue choice with (INFO.Perk census, 2026-10-14).</summary>
            private static readonly string[] CheckPerkFamilies = { "Background", "TRAIT_", "Skill_", "Faction" };

            internal override void GradeBeat(Beat b, int i, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                             Action<string> Fatal, Action<string> Warn)
            {
                string at = $"beat {i + 1} (talk)";
                if (b.model != null || b.name != null || b.replace || b.message != null || b.say != null || b.returnTo != null
                    || b.group != null || b.choose != null)
                    Fatal($"{at} sets model, name, replace, message, say, returnTo, group or choose; a talk beat has none of them "
                          + "(the person is its object and his voice its line), so they would be silently ignored.");
                if (b.person is not Person p) { Fatal($"{at} has no person: nobody to talk to."); }
                else
                {
                    if (string.IsNullOrWhiteSpace(p.name) || string.IsNullOrWhiteSpace(p.template)) Fatal($"{at} person needs both a name and a template.");
                    else if (Tokens(p.name).Any()) Fatal($"{at} person name carries a <Token>; an NPC name is not an alias context.");
                    if (p.company != null) Fatal($"{at} person.company is an ending's; a talk beat's person stands alone, so it would be silently ignored.");
                    if (string.IsNullOrWhiteSpace(p.elevenlabs)) Fatal($"{at} person.elevenlabs is required: the voice his lines are generated in.");
                    if (string.IsNullOrWhiteSpace(p.voice)
                        || !env.LoadOrder.PriorityOrder.WinningOverrides<IVoiceTypeGetter>().Any(v => Allowed(env, t).Contains(v.FormKey.ModKey)
                               && string.Equals(v.EditorID, p.voice, StringComparison.OrdinalIgnoreCase)))
                        Fatal($"{at} person.voice '{p.voice}' is not a VoiceType in {t.mod} or one of its masters: his lines are filed under it.");
                }
                void Spoken(string where, string? text, int max)
                {
                    if (string.IsNullOrWhiteSpace(text)) { Fatal($"{at} {where} is empty."); return; }
                    if (Tokens(text).Any()) Fatal($"{at} {where} carries a <Token>; a voice cannot say one and a scene line is not expanded.");
                    if (text.Length > max) Fatal($"{at} {where} is {text.Length} characters; the limit is {max}.");
                }
                // Schema 2: a voiced line is a LIST of responses said back to back, 1 to ResponsesMax, each checked.
                void SpokenAll(string where, List<string>? said, int max)
                {
                    if (said == null || said.Count == 0) { Fatal($"{at} {where} is empty."); return; }
                    if (said.Count > ResponsesMax) Fatal($"{at} {where} has {said.Count} responses; the limit is {ResponsesMax}.");
                    for (int k = 0; k < said.Count; k++) Spoken(said.Count == 1 ? where : $"{where}[{k}]", said[k], max);
                }
                SpokenAll("greeting", b.greeting, TalkReplyMax);
                if (b.person?.hellos is List<List<string>> hl)
                {
                    if (hl.Count == 0 || hl.Count > TalkHellosMax) Fatal($"{at} person.hellos has {hl.Count} line(s); give 1 to {TalkHellosMax}, or leave it out.");
                    for (int h = 0; h < hl.Count; h++) SpokenAll($"person.hellos[{h}]", hl[h], TalkReplyMax);
                }
                if (b.exchanges == null || b.exchanges.Count == 0) Fatal($"{at} has no exchanges: there is nothing to ask, and exchanges[0] is what ends the talk.");
                else
                    for (int x = 0; x < b.exchanges.Count; x++)
                    {
                        Spoken($"exchanges[{x}].prompt", b.exchanges[x].prompt, TalkPromptMax);
                        SpokenAll($"exchanges[{x}].reply", b.exchanges[x].reply, TalkReplyMax);
                        if (b.exchanges[x].perk is not string pn) continue;
                        if (x == 0)
                        {
                            Fatal($"{at} exchanges[0] has a perk; it is the choice that moves the quest, so a player without '{pn}' could never finish. Put the perk on a side question.");
                            continue;
                        }
                        var allowed = Allowed(env, t);
                        var perk = env.LoadOrder.PriorityOrder.WinningOverrides<IPerkGetter>()
                            .FirstOrDefault(p => allowed.Contains(p.FormKey.ModKey) && string.Equals(p.EditorID, pn, StringComparison.OrdinalIgnoreCase));
                        if (perk == null) Fatal($"{at} exchanges[{x}].perk '{pn}' is not a Perk in {t.mod} or one of its masters.");
                        else if (!CheckPerkFamilies.Any(f => perk.EditorID!.StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                            Warn($"{at} exchanges[{x}].perk '{pn}' is not a background, trait, skill or faction perk; vanilla tags choices only with those, so how the game shows it is unknown.");
                    }
            }

            /// <summary>
            /// A marker, and an alias that CREATES the person there at quest start: a copy of the base's own activator alias
            /// with the cloned NPC as its object (set once he is cloned, in Dialogue). So he stands at his marker before the
            /// player arrives and the objective can point at him from anywhere. ⛔ The marker is a copy of the pattern, never
            /// NewMarker: on the board NewMarker's first call takes the marker source itself, which would move the board's
            /// PrimaryRef onto the talk.
            /// </summary>
            internal override int Slot(BeatsBuild build, int i)
            {
                uint tid = NextAliasId(build.clone);
                var tm = build.markerPattern.DeepCopy(); tm.ID = tid; tm.Name = "DelveBeat" + (i + 1) + "Marker";
                var tp = build.activatorPattern!.DeepCopy(); tp.ID = tid + 1; tp.Name = "DelveBeat" + (i + 1) + "Person";
                tp.CreateReferenceToObject!.AliasID = (short)tm.ID;
                build.clone.Aliases.Add(tm);
                build.clone.Aliases.Add(tp);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)tm.ID, activatorAlias = -1, targetAlias = (int)tp.ID, journalStage = build.beatStage[i] };
                build.talkAlias[i] = tp;
                Console.WriteLine($"  +slot    : beat {i + 1} (talk) -- marker alias {tm.ID}, person {tp.ID} (created at the marker on quest start)");
                return 0;
            }

            /// <summary>No hook: the stage is set by the conversation's closing reply (BuildTalk's SetParentQuestStage).</summary>
            internal override int Hook(BeatsBuild build, int b, int prereq) => 0;

            /// <summary>The person's alias now creates his cloned NPC, and his conversation is built.</summary>
            internal override void Dialogue(BeatsBuild build, int b)
            {
                build.talkAlias[b].CreateReferenceToObject!.Object.SetTo(build.npcOf[b]);
                BuildTalk(build.myMod, build.clone, build.r, b, build.steps, build.beatStage, build.t.entryStage, build.slotOf[b].targetAlias, build.npcOf[b],
                          build.myMod.Npcs.First(n => n.FormKey == build.npcOf[b]).Flags.HasFlag(Npc.Flag.Female), build.bm, build.inputs.TalkPerks);
            }

            /// <summary>
            /// A talk beat's conversation, built INSIDE the Delve's own quest on the scene shapes of
            /// docs/specs/npc_dialogue.md (atbb_mq01, tested in game; NPCDialogueNoun builds the same records for a quest
            /// of its own): a Greeting scene (0x1834) the person opens with, then one Top Level scene per exchange.
            /// exchanges[0] is the Completion topic: its reply sets the beat's stage when it ENDS (the INFO's own
            /// SetParentQuestStage, as NPCDialogueNoun does). EVERY topic scene, the completion too, is 0x2814
            /// (TopLevelTopicsOnEnd) and loops back to the menu. His ruling 2026-10-19, off 15a: "Currently selecting the
            /// complete dialog option completes the quest and ends the dialog. This means if you select it first you can't
            /// hear the other lines." The completion was 0x2810 (closes the talk) until then. So the side questions are
            /// gated only on the step having OPENED, never on it still being open: after the completion they stay askable.
            /// A talk that ends the quest: a stopped quest takes its scenes, so the completing stage does not Stop(); stage
            /// 1000 does, set by DefaultAliasOnUnload on the person when he walks away (BeatsBuild.Stages and Hooks).
            /// His rule: the completion goes FIRST in the menu ("the top dialog choice is the one that progresses the quest, the
            /// lower ones are fleshing out details"). SCPI does it, untied: completion 100, the rest 50/49/48 (his glass,
            /// missionboard02a, 2026-10-14 evening). With every scene at 100 it showed LAST, and neither its flag (0x2810 ->
            /// 0x2814, reverted) nor Index (0/10/20/30 -> 1/2/3/4, kept: vanilla's numbering) moved it.
            /// Every scene is gated on this person (GetIsID) and, past the first step, on the stage that opens it being done.
            /// The greeting and the completion are also gated on the beat's stage NOT done (nothing completes twice, and he
            /// does not re-open with the ask); the side questions are not, so they outlive the completion.
            /// The person's lines are voiced by HIS voice (TalkLines), the player's are silent (WEMFile 0, as in vanilla).
            /// </summary>
            private static void BuildTalk(StarfieldMod myMod, Quest clone, Recipe r, int b, List<Step> steps,
                                          Dictionary<int, int> beatStage, int entryStage, int personAlias, FormKey npc, bool female, BeatsMade bm,
                                          Dictionary<(int beat, int ex), FormKey> talkPerks)
            {
                var tb = r.beats[b];
                int stage = beatStage[b];
                int k = steps.FindIndex(s => s.Beats.Contains(b));
                int enter = k == 0 ? entryStage : steps[k - 1].DoneStage;   // step one opens on the entry stage: 0 on a slate, the board's accept (10)
                string tag = $"{r.id}_talk{b + 1}";
                string vt = tb.person!.voice!, vid = tb.person.elevenlabs!;

                ConditionFloat IsHim()
                {
                    var d = new GetIsIDConditionData();
                    d.FirstParameter = new FormLinkOrIndex<IPlaceableObjectGetter>(d, npc);
                    return new ConditionFloat { Data = d, CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f };
                }
                ConditionFloat StageDone(int st, float want)
                {
                    var d = new GetStageDoneConditionData { RunOnType = Condition.RunOnType.Subject, SecondParameter = st };
                    d.FirstParameter = new FormLinkOrIndex<IQuestGetter>(d, clone.FormKey);
                    return new ConditionFloat { Data = d, CompareOperator = CompareOperator.EqualTo, ComparisonValue = want };
                }
                // A line of his is a list of responses (schema 2); the player's prompt is one silent string.
                (DialogTopic topic, DialogResponses info) Line(string name, List<string> texts, bool his)
                {
                    var topic = new DialogTopic(myMod)
                    {
                        EditorID = name,
                        Category = DialogTopic.CategoryEnum.Scene,
                        Subtype = DialogTopic.SubtypeEnum.CustomScene,
                        SubtypeName = DialogTopic.SubtypeNameEnum.CustomScene,
                    };
                    topic.Quest.SetTo(clone.FormKey);
                    var info = new DialogResponses(myMod) { EditorID = name + "_info", SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low };
                    if (his) info.Speaker.SetTo(npc);
                    // The game plays the file each response's WEMFile names; the first is the topic's id, as the radio lines'
                    // are. Emotion is FFFFFFFF, the "None Reference" (npc_dialogue.md's gotcha).
                    var said = AddResponses(myMod, info, texts, topic.FormKey.ID, his, 7.466667f);
                    topic.Responses.Add(info);
                    // TPIC: the topic's own list of its infos. Missing, the CK crashes on a click (book_audio.md).
                    topic.TopicInfoList = new Noggog.ExtendedList<IFormLinkGetter<IDialogResponsesGetter>> { info.ToLink<IDialogResponsesGetter>() };
                    clone.DialogTopics.Add(topic);
                    foreach (var (wem, text) in said) bm.TalkLines.Add((wem, text, vt, vid, female));
                    return (topic, info);
                }
                Scene NewScene(string name, uint flags, bool untilDone)
                {
                    var sc = new Scene(myMod)
                    {
                        EditorID = name,
                        Flags = (Scene.Flag)flags,
                        VNAM = new byte[] { 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0, 3, 0, 0, 0 },
                    };
                    sc.Quest.SetTo(clone.FormKey);
                    sc.Conditions.Add(IsHim());
                    if (untilDone) sc.Conditions.Add(StageDone(stage, 0f));
                    if (enter > 0) sc.Conditions.Add(StageDone(enter, 1f));
                    sc.Actors.Add(new SceneActor { ID = (uint)personAlias, BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
                    sc.Actors.Add(new SceneActor { ID = unchecked((uint)-2), BehaviorFlags = (SceneActor.BehaviorFlag)266, Flags = SceneActor.Flag.NoCommandState });
                    clone.Scenes.Add(sc);
                    return sc;
                }

                var (gt, _) = Line(tag + "_greet", tb.greeting!, true);
                var gs = NewScene(tag + "_greet_scene", 0x00001834u, true);
                gs.Phases.Add(new ScenePhase { Name = "Greeting", EditorWidth = 298 });
                var ga = new DialogueSceneAction { Index = 1, AliasID = personAlias, StartPhase = 0, EndPhase = 0 };
                ga.Topic.SetTo(gt.FormKey);
                gs.Actions = new Noggog.ExtendedList<ASceneAction> { ga };
                Console.WriteLine($"  +talk    : {gs.EditorID} greets  \"{string.Join(" / ", tb.greeting!)}\"");

                for (int x = 0; x < tb.exchanges!.Count; x++)
                {
                    var ex = tb.exchanges[x];
                    var (pt, pi) = Line($"{tag}_ask{x + 1}", new List<string> { ex.prompt! }, false);
                    // A [Background] / [Skill] / [Trait] choice: vanilla's INFO.Perk on the PLAYER's line, no condition with it.
                    if (talkPerks.TryGetValue((b, x), out var perkKey)) pi.Perk.SetTo(perkKey);
                    var (nt, ni) = Line($"{tag}_reply{x + 1}", ex.reply!, true);
                    if (x == 0) ni.SetParentQuestStage = new DialogSetParentQuestStage { OnEnd = (short)stage };
                    var sc = NewScene($"{tag}_topic{x + 1}_scene", 0x00002814u, x == 0);
                    sc.Index = (uint)(x + 1);   // vanilla's numbering, from 1; NOT shown to be the menu's sort key (see above)
                    // SCPI orders the menu, highest first, and must not tie (vanilla never does: MQ101 90/80/50/30/20). The
                    // completion takes 100, the rest 50, 49, 48... in recipe order. Tied at 100, the completion showed last.
                    sc.SCPI = BitConverter.GetBytes((ushort)(x == 0 ? 100 : 51 - x));
                    sc.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
                    sc.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
                    var pa = new DialogueSceneAction { Index = 3, AliasID = -2, StartPhase = 0, EndPhase = 0 };
                    pa.Topic.SetTo(pt.FormKey);
                    var na = new DialogueSceneAction { Index = 4, AliasID = personAlias, StartPhase = 1, EndPhase = 1 };
                    na.Topic.SetTo(nt.FormKey);
                    sc.Actions = new Noggog.ExtendedList<ASceneAction> { pa, na };
                    Console.WriteLine($"  +talk    : {sc.EditorID} \"{ex.prompt}\"" + (x == 0 ? $"  (sets {stage} when the reply ends, loops to the menu)" : "  (loops to the menu, askable after the completion)")
                                      + (ex.perk != null ? $"  [{ex.perk}]" : ""));
                }
                bm.Talks.Add((b, stage, personAlias, npc, tb.exchanges.Count, enter > 0 ? 3 : 2));   // the greeting's and the completion's; a side question has one fewer

                // What he says as the player WALKS UP (person.hellos, Jessica's; his 2026-10-19). Vanilla's shape, copied off
                // MQ101 (Lin, Heller): ONE Hello topic per quest (Misc / Hello, topic priority 50), a group INFO per person
                // (flags 256) carrying the gate, GetIsID(him), and the spoken lines under it (flags 1024, DialogGroup = the
                // group), one picked per walk-up. Many lines share one topic here, so each line's WEMFile is its OWN id
                // (NPCDialogueNoun's scheme; manual 39: either works if the file is named the same), never the topic's.
                // ⚠ Unproven at his glass: built on vanilla's numbers, not on a play.
                if (tb.person!.hellos is List<List<string>> hellos)
                {
                    string htag = $"{r.id}_hellos";
                    var ht = clone.DialogTopics.FirstOrDefault(d => d.EditorID == htag);
                    if (ht == null)
                    {
                        ht = new DialogTopic(myMod)
                        {
                            EditorID = htag,
                            Category = DialogTopic.CategoryEnum.Misc,
                            Subtype = DialogTopic.SubtypeEnum.Hello,
                            SubtypeName = DialogTopic.SubtypeNameEnum.Hello,
                            Priority = 50,
                        };
                        ht.Quest.SetTo(clone.FormKey);
                        ht.TopicInfoList = new Noggog.ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>();
                        clone.DialogTopics.Add(ht);
                    }
                    var grp = new DialogResponses(myMod) { EditorID = $"{tag}_hellos", Flags = new DialogResponseFlags { Flags = 256 } };
                    grp.Conditions.Add(IsHim());
                    var lines = new List<DialogResponses>();
                    for (int h = 0; h < hellos.Count; h++)
                    {
                        var hi = new DialogResponses(myMod)
                        {
                            EditorID = $"{tag}_hello{h + 1}",
                            Flags = new DialogResponseFlags { Flags = 1024 },
                            SubtitlePriority = DialogResponses.SubtitlePriorityLevel.Low,
                        };
                        hi.DialogGroup.SetTo(grp.FormKey);
                        // Each walk-up line is itself a list of responses (schema 2); its first takes the INFO's own id.
                        foreach (var (wem, text) in AddResponses(myMod, hi, hellos[h], hi.FormKey.ID, true, 7.466667f))
                            bm.TalkLines.Add((wem, text, vt, vid, female));
                        lines.Add(hi);
                    }
                    // Vanilla lists the spoken lines before their group (MQ101: 00563E.. then 00713D).
                    foreach (var hi in lines.Append(grp))
                    {
                        ht.Responses.Add(hi);
                        ht.TopicInfoList!.Add(hi.ToLink<IDialogResponsesGetter>());
                    }
                    bm.Hellos.Add((b, npc, hellos.Count));
                    Console.WriteLine($"  +hello   : {grp.EditorID}, {hellos.Count} walk-up line(s) in his voice, gated GetIsID({npc})");
                }
            }
        }
    }
}
