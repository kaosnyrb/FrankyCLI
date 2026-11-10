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
        /// ask. exchanges[0] closes the conversation and sets the beat's stage; on the talk that ends a ground board Delve, an
        /// exchange carrying "ends" closes it too, on an ending of its own (MB93d, TalkEndings). It has no object, no box and no radio line:
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

            /// <summary>A give's tag, the literal words vanilla opens the player's line with: "[Give Med Pack] ", "[Give 3 Addichrone] ".</summary>
            internal static string GiveTag(string name, int count) => count > 1 ? $"[Give {count} {name}] " : $"[Give {name}] ";

            // OT-9A, a wounded person: vanilla's forms, every one read off OE_BryanB_FamilyInNeed (gen_inspect qalias / qall,
            // 2026-11-10) except the package, which is the generic one (no OwnerQuest; it sits only while SQ_WoundedState reads
            // wounded, so HealActor's state change is what stands him up).
            internal static readonly FormKey WoundedFurniture = FormKey.Factory("02296A:Starfield.esm");      // NPCWoundedLean
            internal static readonly FormKey CaptiveFurnitureLink = FormKey.Factory("1D3036:Starfield.esm");  // SQ_Link_CaptiveFurniture
            internal static readonly FormKey CaptiveSitPackage = FormKey.Factory("1D3037:Starfield.esm");     // SQ_Captive_Sit
            internal static readonly FormKey NoBlockingHellos = FormKey.Factory("1FCA3D:Starfield.esm");      // SQ_CaptiveStateNoBlockingHellos (AV)
            internal const string WoundedScript = "DefaultCaptiveWoundedAlias";

            /// <summary>DefaultCaptiveWoundedAlias as FamilyInNeed's WoundedSibling carries it: the ten autofill properties, no
            /// StageToSetWhenHealed (the talk's closer sets the stage; HealActor is called from it).</summary>
            internal static ScriptEntry WoundedAliasScript()
            {
                var sc = new ScriptEntry { Name = WoundedScript };
                foreach (var (n, fk) in new[]
                {
                    ("SQ_Link_CaptiveFurniture", "1D3036"), ("WoundedFaction", "0578FF"),
                    ("SQ_Wounded_DialogueSubtype_HealedActorGratitude", "05E832"), ("SQ_Captive", "1D3081"),
                    ("SQ_WoundedState_1_Wounded", "05E834"), ("SQ_WoundedState_0_Unset", "05E835"),
                    ("SQ_WoundedState", "05E836"), ("SQ_WoundedState_2_Healed", "05E833"),
                    ("SQ_Captive_FurnitureType_Wounded", "057902"), ("CaptiveFaction", "03E0C8"),
                })
                    sc.Properties.Add(new ScriptObjectProperty { Name = n, Flags = ScriptProperty.Flag.Edited,
                                                                 Object = new FormLink<IStarfieldMajorRecordGetter>(FormKey.Factory(fk + ":Starfield.esm")) });
                return sc;
            }

            // OT-9A, a CAPTIVE person: vanilla's board rescue, MB_Rescue01_FCR05's Prisoner (gen_inspect qall, 2026-11-10), with the
            // ground furniture where it has the ship one (NPCSitFloorCaptive_Kneeling_ShipPrisoner, 2108FC). Its stage 100 calls
            // FreePrisoner() from the quest, as our closer's stage does. The stock "Free" perk would send the same event, but the
            // vanilla board mission frees by talk, and so do we.
            internal static readonly FormKey CaptiveFurniture = FormKey.Factory("047707:Starfield.esm");     // NPCSitFloorCaptive_Kneeling
            internal const string CaptiveScript = "DefaultCaptiveAlias";
            internal static readonly FormKey BoundFaction = FormKey.Factory("058610:Starfield.esm");          // BoundFaction

            /// <summary>DefaultCaptiveAlias as MB_Rescue01_FCR05's Prisoner carries it: eleven forms and two bools, no StageToSetWhenFreed.</summary>
            internal static ScriptEntry CaptiveAliasScript()
            {
                var sc = new ScriptEntry { Name = CaptiveScript };
                foreach (var (n, fk) in new[]
                {
                    ("SQ_CaptiveState", "1C3960"), ("SQ_Link_CaptiveFurniture", "1D3036"), ("SQ_Followers", "00D773"),
                    ("SQ_Captive_DialogueSubtype_FreedPrisonerGratitude", "148909"), ("SQ_CaptiveState_1_Captive", "1C395E"),
                    ("SQ_CaptiveState_0_Unset", "1C395F"), ("SQ_CaptiveState_2_Freed", "1C395D"), ("BoundFaction", "058610"),
                    ("SQ_Captive_FurnitureType_Bound", "057903"), ("SQ_Captive", "1D3081"), ("CaptiveFaction", "03E0C8"),
                })
                    sc.Properties.Add(new ScriptObjectProperty { Name = n, Flags = ScriptProperty.Flag.Edited,
                                                                 Object = new FormLink<IStarfieldMajorRecordGetter>(FormKey.Factory(fk + ":Starfield.esm")) });
                sc.Properties.Add(new ScriptBoolProperty { Name = "DeleteWhenCleanedUp", Flags = ScriptProperty.Flag.Edited, Data = false });
                sc.Properties.Add(new ScriptBoolProperty { Name = "AlsoRemoveFromCaptiveFaction", Flags = ScriptProperty.Flag.Edited, Data = true });
                return sc;
            }

            /// <summary>
            /// A person HELD until the talk's closer releases him (OT-9A): what differs between wounded and captive, each read off
            /// its vanilla source. AtFurniture: he is created AT the furniture (FamilyInNeed) or at the marker beside it
            /// (MB_Rescue01). Factions: put on the ALIAS (vanilla's), so the cloned NPC record stays faction-free.
            /// </summary>
            internal sealed record Held(string Kind, string Source, FormKey Furniture, string FurnitureName, bool AtFurniture,
                                        string Script, Func<ScriptEntry> MakeScript, int ObjectProps, string Release, FormKey[] Factions);
            internal static readonly Held WoundedHeld = new("wounded", "OE_BryanB_FamilyInNeed", WoundedFurniture, "NPCWoundedLean", true,
                WoundedScript, WoundedAliasScript, 10, "HealActor()", Array.Empty<FormKey>());
            internal static readonly Held CaptiveHeld = new("captive", "MB_Rescue01_FCR05", CaptiveFurniture, "NPCSitFloorCaptive_Kneeling", false,
                CaptiveScript, CaptiveAliasScript, 11, "FreePrisoner()",
                new[] { FormKey.Factory("03E0C8:Starfield.esm"), FormKey.Factory("106C30:Starfield.esm") });   // CaptiveFaction, PlayerAllyFaction
            internal static Held? HeldOf(Person? p) => p?.wounded == true ? WoundedHeld : p?.captive == true ? CaptiveHeld : null;

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
                if (b.at.Length == 0 && !IsBoardSpace(t)) Fatal($"{at} has no at: the marker he stands on (only a rescue's talks happen aboard, with none).");
                if (b.rider != null)
                {
                    // A rescue's goodbye: the check-in's person, and its one line ends the mission, so a side question could not be heard after it.
                    if (!IsBoardSpace(t)) Fatal($"{at} sets rider; a talk reuses an earlier person on a rescue only.");
                    if (b.person != null) Fatal($"{at} sets rider and person; the rider is its person.");
                    if (b.exchanges?.Count != 1) Fatal($"{at} is a goodbye with {b.exchanges?.Count ?? 0} exchanges; it has one, because its reply ends the mission.");
                }
                else if (b.person is not Person p) { Fatal($"{at} has no person: nobody to talk to."); }
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
                        if (b.exchanges[x].give is Give gv)
                        {
                            // A rescue's talks are built by the ride path (RideBeat), which passes no gives: refused, never dropped.
                            if (IsBoardSpace(t)) { Fatal($"{at} exchanges[{x}].give is on a rescue's talk, and the rescue build writes no give."); continue; }
                            // Allowed on exchanges[0] (his ruling 2026-11-09), unlike a perk: the item can be fetched, a perk cannot.
                            if (string.IsNullOrWhiteSpace(gv.item)) Fatal($"{at} exchanges[{x}].give has no item: nothing to hand over.");
                            else if (gv.count < 1) Fatal($"{at} exchanges[{x}].give.count is {gv.count}; it is how many he hands over, 1 or more.");
                            else
                            {
                                var allowedG = Allowed(env, t);
                                var item = env.LoadOrder.PriorityOrder.WinningOverrides<IItemGetter>()
                                    .FirstOrDefault(it => allowedG.Contains(it.FormKey.ModKey) && string.Equals(it.EditorID, gv.item, StringComparison.OrdinalIgnoreCase));
                                string? nm = (item as Mutagen.Bethesda.Plugins.Aspects.ITranslatedNamedGetter)?.Name?.String;
                                if (item == null) Fatal($"{at} exchanges[{x}].give.item '{gv.item}' is not an item in {t.mod} or one of its masters.");
                                else if (string.IsNullOrWhiteSpace(nm)) Fatal($"{at} exchanges[{x}].give.item '{gv.item}' has no in-game name, and the [Give ...] tag is its name.");
                                else Spoken($"exchanges[{x}].prompt with its tag", GiveTag(nm, gv.count) + b.exchanges[x].prompt, TalkPromptMax);
                            }
                        }
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
                // OT-9A: a wounded person is healed by the closer's stage (Fragment's HealActor), as vanilla's two wounded
                // quests take a med pack and then heal. So the closer hands something over, and there is ONE closer.
                // A captive is freed the same way (FreePrisoner on the closer's stage, MB_Rescue01's stage 100) and needs no give.
                if (b.person?.wounded == true && b.person.captive)
                    Fatal($"{at} person is both wounded and captive; vanilla's own scripts refuse that pair (DefaultCaptiveAlias: \"Dual use ... is not currently supported\").");
                else if (HeldOf(b.person) is Held h)
                {
                    if (IsBoardSpace(t)) Fatal($"{at} person.{h.Kind} on a rescue; a rescue's person rides the ship, she is not held anywhere.");
                    if (h == WoundedHeld && b.exchanges is { Count: > 0 } && b.exchanges[0].give == null)
                        Fatal($"{at} person.wounded and exchanges[0] gives nothing: the heal is the hand-over (both vanilla wounded quests take a med pack), so give exchanges[0] an item.");
                    if (b.exchanges?.Any(e => e.ends != null) == true)
                        Fatal($"{at} person.{h.Kind} with an exchange that ends: the release is written on the beat's own stage only, so a second closer would leave him held.");
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
                // OT-9A, a HELD person (wounded or captive): his vanilla source's furniture alias and person alias, copied. The
                // furniture is created at the marker and goes before him in the list (fill order is list order); a wounded man is
                // created AT it (FamilyInNeed), a captive at the marker beside it (MB_Rescue01). Either way the link snaps him in.
                QuestReferenceAlias? tf = null;
                var held = HeldOf(build.r.beats[i].person);
                if (held != null)
                {
                    tf = build.activatorPattern!.DeepCopy(); tf.ID = tid + 2; tf.Name = "DelveBeat" + (i + 1) + "HeldFurniture";
                    tf.CreateReferenceToObject!.AliasID = (short)tm.ID;
                    tf.CreateReferenceToObject.Object.SetTo(held.Furniture);
                    if (held.AtFurniture) tp.CreateReferenceToObject.AliasID = (short)tf.ID;
                    // The activator pattern carries no LinkedAliases list, so it is made here (null on the copy, 2026-11-10's dry run).
                    tp.LinkedAliases ??= new Noggog.ExtendedList<LinkedAlias>();
                    tp.LinkedAliases.Add(new LinkedAlias { AliasID = (int)tf.ID, Keyword = new FormLinkNullable<IKeywordGetter>(CaptiveFurnitureLink) });
                    tp.PackageData.Add(new FormLink<IPackageGetter>(CaptiveSitPackage));
                    foreach (var f in held.Factions) tp.Factions.Add(new FormLink<IFactionGetter>(f));
                    build.clone.Aliases.Add(tf);
                    var entry = NewAliasEntry(build.vma, build.clone.FormKey, (int)tp.ID);
                    var hs = held.MakeScript();
                    // His 2026-11-10 "lets do B ... Both will progress the stage", after f1: vanilla's own "Set Free" prompt reaches
                    // DefaultCaptiveAlias (the perk sends CaptiveSetFree), so the stock script's own StageToSetWhenFreed is the
                    // beat's stage, and freeing him by the prompt completes the beat as the talk does. MB_Rescue01 leaves it -1
                    // (talk only); ours is the other vanilla setting of the same property. Wounded stays -1: no prompt heals.
                    if (held == CaptiveHeld)
                        hs.Properties.Add(new ScriptIntProperty { Name = "StageToSetWhenFreed", Flags = ScriptProperty.Flag.Edited, Data = build.beatStage[i] });
                    entry.Scripts.Add(hs);
                }
                build.clone.Aliases.Add(tp);
                build.slotOf[i] = new BeatSlot { markerAlias = (int)tm.ID, activatorAlias = -1, targetAlias = (int)tp.ID, journalStage = build.beatStage[i] };
                build.talkAlias[i] = tp;
                Console.WriteLine($"  +slot    : beat {i + 1} (talk) -- marker alias {tm.ID}, person {tp.ID} (created at the marker on quest start)"
                                  + (tf != null ? $"; {held!.Kind.ToUpper()} ({held.Source}): furniture {tf.ID} {held.FurnitureName} at the marker, he is created at {(held.AtFurniture ? "it" : "the marker")}, linked by SQ_Link_CaptiveFurniture, SQ_Captive_Sit, {held.Script}" : ""));
                return 0;
            }

            /// <summary>No hook: the stage is set by the conversation's closing reply (BuildTalk's SetParentQuestStage).</summary>
            internal override int Hook(BeatsBuild build, int b, int prereq) => 0;

            /// <summary>The person's alias now creates his cloned NPC, and his conversation is built.</summary>
            internal override void Dialogue(BeatsBuild build, int b)
            {
                build.talkAlias[b].CreateReferenceToObject!.Object.SetTo(build.npcOf[b]);
                BuildTalk(build.myMod, build.clone, build.r, b, build.steps, build.beatStage, build.t.entryStage, build.slotOf[b].targetAlias, build.npcOf[b],
                          build.myMod.Npcs.First(n => n.FormKey == build.npcOf[b]).Flags.HasFlag(Npc.Flag.Female), build.bm, build.inputs.TalkPerks,
                          build.inputs.TalkGives);
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
            internal static void BuildTalk(StarfieldMod myMod, Quest clone, Recipe r, int b, List<Step> steps,
                                          Dictionary<int, int> beatStage, int entryStage, int personAlias, FormKey npc, bool female, BeatsMade bm,
                                          Dictionary<(int beat, int ex), FormKey> talkPerks,
                                          Dictionary<(int beat, int ex), (FormKey item, string name, int count)> talkGives)
            {
                var tb = r.beats[b];
                int stage = beatStage[b];
                int k = steps.FindIndex(s => s.Beats.Contains(b));
                int enter = k == 0 ? entryStage : steps[k - 1].DoneStage;   // step one opens on the entry stage: 0 on a slate, the board's accept (10)
                string tag = $"{r.id}_talk{b + 1}";
                var who = tb.person ?? r.beats[tb.rider!.Value - 1].person!;   // a rescue's goodbye is the check-in's person again
                string vt = who.voice!, vid = who.elevenlabs!;
                // The closers: exchanges[0] on the beat's stage, and on the talk that ends the Delve every exchange carrying
                // "ends" on its own ending stage (MB93d; TalkEndings, written in Stages). One closer is the shape as it was.
                var closes = HandBackTalk(r, steps) == b && bm.TalkEnds.Count > 0
                    ? bm.TalkEnds.ToDictionary(e => e.ex, e => e.stage) : new Dictionary<int, int> { [0] = stage };

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
                    // Until ANY ending is done: with two closers, either one takes the greeting and both closers away.
                    if (untilDone) foreach (var end in closes.Values) sc.Conditions.Add(StageDone(end, 0f));
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
                    bool gives = talkGives.TryGetValue((b, x), out var give);
                    var (pt, pi) = Line($"{tag}_ask{x + 1}", new List<string> { (gives ? TalkBeat.GiveTag(give.name, give.count) : "") + ex.prompt! }, false);
                    // A [Background] / [Skill] / [Trait] choice: vanilla's INFO.Perk on the PLAYER's line, no condition with it.
                    if (talkPerks.TryGetValue((b, x), out var perkKey)) pi.Perk.SetTo(perkKey);
                    // A give (plan OT-34): the exchange offered only while the player carries the count (GetItemCount run on
                    // PlayerRef, Barrett's [Give Med Pack]), and duo_delve_give on the player's line takes it when the line
                    // ends. The tag is already in the words above.
                    // ⛔ THE GATE IS ON THE SCENE AND THE PLAYER'S LINE BOTH (his plays of MB85a, 2026-11-09). On the line alone
                    // (vanilla's place, Barrett): with no pack the entry still showed, "No Dialogue Action on Player in First
                    // Phase", and picking it passed the mission, because our menu entry is the exchange's own Top Level scene.
                    // On the scene alone, the line left with no condition: the board NEVER DEALT the quest, on two builds (the
                    // item gate, then a harmless dummy in its place), while a rebuild of the line-only shape dealt and played,
                    // so a rebuild is not the cause. Both places: dealt, the entry gone with no pack and there with one, his
                    // "yup works as expected" (13:2x). So the 4th scene condition is fine, and what stopped the board starting the
                    // quest was the player's line carrying duo_delve_give with NO condition. Why, the records cannot say; a
                    // board quest that fails to start writes nothing to the Papyrus log. Keep the two together.
                    ConditionFloat? itemGate = null;
                    if (gives)
                    {
                        var ic = new GetItemCountConditionData { RunOnType = Condition.RunOnType.Reference };
                        ic.Reference.SetTo(PlayerRef);
                        ic.FirstParameter = new FormLinkOrIndex<IPlaceableObjectGetter>(ic, give.item);
                        itemGate = new ConditionFloat { Data = ic, CompareOperator = CompareOperator.GreaterThanOrEqualTo, ComparisonValue = give.count };
                        var gs2 = new ScriptEntry { Name = "duo_delve_give" };
                        gs2.Properties.Add(new ScriptObjectProperty { Name = "Item", Flags = ScriptProperty.Flag.Edited, Object = new FormLink<IStarfieldMajorRecordGetter>(give.item) });
                        gs2.Properties.Add(new ScriptIntProperty { Name = "ItemCount", Flags = ScriptProperty.Flag.Edited, Data = give.count });
                        var va = new DialogResponsesAdapter { Version = 6, ObjectFormat = 2 };   // vanilla's, on all 11 DefaultTopicRemoveItem lines (itemcheck)
                        va.Scripts.Add(gs2);
                        pi.VirtualMachineAdapter = va;
                        bm.Gives.Add((b, x, give.item, give.count, TalkBeat.GiveTag(give.name, give.count) + ex.prompt!));
                    }
                    var (nt, ni) = Line($"{tag}_reply{x + 1}", ex.reply!, true);
                    bool closer = closes.TryGetValue(x, out int endStage);
                    if (closer) ni.SetParentQuestStage = new DialogSetParentQuestStage { OnEnd = (short)endStage };
                    var sc = NewScene($"{tag}_topic{x + 1}_scene", 0x00002814u, closer);
                    if (itemGate != null) { sc.Conditions.Add(itemGate); pi.Conditions.Add(itemGate.DeepCopy()); }
                    sc.Index = (uint)(x + 1);   // vanilla's numbering, from 1; NOT shown to be the menu's sort key (see above)
                    // SCPI orders the menu, highest first, and must not tie (vanilla never does: MQ101 90/80/50/30/20). The
                    // completion takes 100, the rest 50, 49, 48... in recipe order. Tied at 100, the completion showed last.
                    // A further closer (MB93d) sits with the completion at the top, under it in recipe order: 99, 98...
                    sc.SCPI = BitConverter.GetBytes((ushort)(x == 0 ? 100 : closer ? 100 - closes.Keys.Count(k => k > 0 && k <= x) : 51 - x));
                    sc.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
                    sc.Phases.Add(new ScenePhase { Name = "", EditorWidth = 350 });
                    var pa = new DialogueSceneAction { Index = 3, AliasID = -2, StartPhase = 0, EndPhase = 0 };
                    pa.Topic.SetTo(pt.FormKey);
                    var na = new DialogueSceneAction { Index = 4, AliasID = personAlias, StartPhase = 1, EndPhase = 1 };
                    na.Topic.SetTo(nt.FormKey);
                    sc.Actions = new Noggog.ExtendedList<ASceneAction> { pa, na };
                    Console.WriteLine($"  +talk    : {sc.EditorID} \"{ex.prompt}\"" + (closer ? $"  (sets {endStage} when the reply ends, loops to the menu)" : "  (loops to the menu, askable after the completion)")
                                      + (ex.perk != null ? $"  [{ex.perk}]" : "") + (gives ? $"  gives {give.count} x {give.item} ({give.name}), offered only while carried" : ""));
                }
                bm.Talks.Add((b, stage, personAlias, npc, tb.exchanges.Count, enter > 0 ? 3 : 2));   // the greeting's and the completion's; a side question has one fewer

                // What he says as the player WALKS UP (person.hellos, Jessica's; his 2026-10-19). ONE Hello topic per quest
                // (Misc / Hello, topic priority 50), a group INFO per person carrying the gate, and the spoken lines under it
                // (DialogGroup = the group), one picked per walk-up. MQ101's placed-Lin shape: the group flags 256 gated
                // GetIsID(him), the lines flags 1024. Many lines share one topic, so each line's WEMFile is its OWN id
                // (NPCDialogueNoun's scheme; manual 39: either works if the file is named the same), never the topic's.
                // ⛔ THE GROUP CARRIES THE RECORD HEADER InfoGroup, AND THAT IS THE FIX (his play of 14a, 2026-10-23: "BOOM
                // worked", his line heard in his voice). Every vanilla walk-up group has it; without it the engine falls back
                // to a silent "Captain", under this shape and under vanilla's created-speaker one (the alias gate, group 768,
                // lines 2: ca77155, played on 19a 2026-10-22, no change). This is the shape that PLAYED with the header, set in
                // place by gen_delve infogroup; the alias shape with the header is unplayed, so it is not what a build writes.
                if (tb.person?.hellos is List<List<string>> hellos)
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
                    var grp = new DialogResponses(myMod) { EditorID = $"{tag}_hellos", Flags = new DialogResponseFlags { Flags = 256 },
                                                           MajorFlags = DialogResponses.MajorFlag.InfoGroup };
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
                    Console.WriteLine($"  +hello   : {grp.EditorID}, {hellos.Count} walk-up line(s) in his voice, an InfoGroup gated GetIsID({npc})");
                }
            }
        }
    }
}
