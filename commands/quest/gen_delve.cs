using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Aspects;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Nouns.QuestAuthoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FrankyCLI
{
    /// <summary>
    /// DELVES FROM DATA. A recipe is a JSON file an author writes; a template is a row saying which
    /// shipped quest shape can carry it; this command lints one against the other and builds it.
    ///
    ///   gen_delve templates                     list the registry
    ///   gen_delve lint  <recipe.json | id>      grade a recipe without writing anything
    ///   gen_delve lint  --all                   grade every recipe, one load-order index, one line each
    ///   gen_delve build <recipe.json | id> [--dry]   lint, then construct + rewire + EMIT THE MODEL + verify OFF DISK
    ///   gen_delve build <id> --dry --emit <dir> [--from-base]   the A/B proof's output: plugin and model to <dir>
    ///   gen_delve shell <template>              write a template's shell model from its base quest, once
    ///   gen_delve keywords [filter ...]         theme tally inside a narrowed POI pool
    ///
    /// ⭐ THE FORMAT, EVERY FIELD AND EVERY RULE: docs/quests/delve-recipes.md. Two template kinds, both built by
    /// gen_delve_beats.cs with no driver: beats (a slate Delve) and board (a mission-board one). Retired: delve4
    /// (carry-absence-recover-return) on 2026-10-09, when 03 to 05 played the same on beats; dualactivator
    /// (two-beat-one-place) and choice (find-owner-or-buyer) on 2026-10-15, with no recipe on either since 10-09.
    ///
    /// ⭐ THE DIVISION IS HIS AND IT IS THE POINT: the recipe is content and belongs to whoever
    /// writes the mission; the template is mechanism and belongs to whoever reads records. Adding a
    /// hundred Delves adds a hundred recipes and no code. Adding a new KIND of beat adds a template
    /// row and, usually, nothing else either.
    ///
    /// ⛔ WHY A TEMPLATE IS NEEDED AT ALL, because it looks like ceremony until you hit it: a Delve is built
    /// from a shipped quest's SHAPE (its shell: aliases, stages, the place and marker slots), and the row says
    /// where each moving part is in it, read off the record. The objective layer is PAPYRUS-ONLY (of 245
    /// vanilla Default* scripts exactly one calls SetObjectiveDisplayed); since 2026-10-09 that half is a
    /// fragment script this tool writes per Delve, not a driver a row names, so no row carries one.
    ///
    /// ⭐ THE SELECTION CONDITIONS ARE DERIVED, NEVER AUTHORED. A recipe says which marker each beat
    /// sits on. The place's LocationHasRefType conditions are then the UNION of those markers plus
    /// MapMarkerRefType, computed here. An author cannot put a beat on a marker the place was not
    /// selected for, because there is no field in which to disagree -- the class of bug where a
    /// quest picks a POI on one marker and then hunts for another is unconstructible rather than
    /// checked.
    ///
    /// ⚠ ONE STATED LIMIT, v1: `place.leash` is ADVISORY. A recipe naming a leash gets a WARN rather than a
    /// rewrite, because constructing a GetDistance condition has not been proven the way LocationHasRefType
    /// has. The MAIN place keeps the base's condition verbatim (within duo_samemapdistance of the player).
    /// The second and third places do NOT, since 2026-10-13: a beats build re-anchors their copied leash to
    /// duo_longdrivedistance of the main place's map marker (and the third also of a marker in the second),
    /// changing only the copy's alias parameter and global (LeashPlaces, gen_delve_beats.cs; his "make all
    /// points within 7500"). Named here rather than discovered by someone whose leash quietly did nothing.
    /// </summary>
    public static partial class gen_delve
    {
        // ------------------------------------------------------------------ models

        private sealed class Template
        {
            public string id { get; set; } = "";
            /// <summary>
            /// "beats" or "board": both remove the base's driver and have none. Anything else is refused when the
            /// registry loads, never defaulted, since the kinds that once took other build paths are gone.
            /// </summary>
            public string kind { get; set; } = "";
            public string @base { get; set; } = "";
            /// <summary>
            /// TRUE CONSTRUCTION (his ruling, 2026-10-15): the quest a build starts from, as a model file under
            /// data/delves (gen_questmodel's format), written once by `gen_delve shell` and owned from then on. With it the
            /// build makes its quest with `new` from this file and never copies the base quest; `base` then names only
            /// where the shell came from (and what the lint still reads, named as open). Without it, the old clone.
            /// </summary>
            public string? shell { get; set; }
            /// <summary>
            /// The shell's companion: every alias this template's build takes off ANOTHER quest (remotePlaceFrom's request,
            /// borrowFrom's aliases, vanilla's wave collection), as models under data/delves, written by `gen_delve shell`
            /// with the shell and owned from then on. With both, a build reads no quest at all (his 2026-10-15 "close the 2
            /// places"). Null = the build reads those quests, as it did before.
            /// </summary>
            public string? parts { get; set; }
            public string mod { get; set; } = "";
            public string driver { get; set; } = "";
            /// <summary>The base's script entry that is removed, and whose values are taken off it.</summary>
            public string? replacesDriver { get; set; }
            /// <summary>beats: fewest who stand with a recover beat's holder. The most is taken off the base's driver.</summary>
            public int gangMin { get; set; }
            /// <summary>beats: the base's alias filled with the player, which a recover beat's OnItemAdded hook sits on. -1 = none.</summary>
            public int playerAlias { get; set; } = -1;
            /// <summary>beats: the stage a recipe's approach sets (below the first beat's 10). -1 = no approach on this template.</summary>
            public int approachStage { get; set; } = -1;
            public int beats { get; set; }
            public bool carriesItem { get; set; }
            public int placeAlias { get; set; }
            public int mapMarkerAlias { get; set; }
            public int briefingStage { get; set; }
            public int completeStage { get; set; }
            public List<BeatSlot> beatSlots { get; set; } = new();
            public int maxBeats { get; set; }
            public Dictionary<string, string> tokens { get; set; } = new();
            /// <summary>
            /// The base's OTHER place alias, which a beat may name with "place": "second" to happen at a
            /// different POI. -1 = this template has none. His ruling 2026-09-24 on duo_delve03: the load
            /// "should have been at a different POI and then I take it to that one".
            /// </summary>
            public int secondPlaceAlias { get; set; } = -1;
            /// <summary>
            /// A THIRD place the base does not have, CREATED by cloning one of its location aliases. Null = this
            /// template has none. His ruling 2026-10-08, "third place, time to learn": the medal's owner and buyer
            /// are two different peopled sites.
            /// </summary>
            public ThirdPlace? thirdPlace { get; set; }
            /// <summary>
            /// beats: the quest whose ORBIT chain a recipe's place.third.orbit copies (his MB01 satellite repair, 2026-10-22,
            /// his "go on 19a, try over"): an orbit location alias, the levelled space cell created at it, a marker filled from
            /// inside that cell, and the cell's map marker. Read once into the parts file. Null = this template has no orbit.
            /// </summary>
            public string? orbitFrom { get; set; }
            public OrbitAliases? orbitAliases { get; set; }
            /// <summary>
            /// beats: a quest in the same mod whose place alias (the same ID as placeAlias) is a REMOTE request, copied
            /// over the main place when a recipe sets place.remote. Null = this template cannot go remote. His
            /// 2026-10-14: "I got sick of travelling", so a Delve whose whole site is requested on another planet.
            /// </summary>
            public string? remotePlaceFrom { get; set; }
            /// <summary>
            /// beats and board: the stage the mission BEGINS on, where step one spawns, shows its objective, resets its
            /// counters and speaks its broadcast. 0 = run on start (a slate Delve starts when it is dealt). A board quest
            /// starts when the board refreshes, long before it is taken, so its entry is the board's AcceptStage (10).
            /// </summary>
            public int entryStage { get; set; }
            /// <summary>
            /// board: the quest-level script that replaces the removed driver (vanilla's MissionQuestScript, which the
            /// board talks to), carrying the driver's properties named in questScriptKeeps. Null = the beats kind's
            /// own turn-down handler. Set, the last stage pays through it (MissionComplete) rather than CompleteQuest.
            /// </summary>
            public string? questScript { get; set; }
            public List<string> questScriptKeeps { get; set; } = new();
            /// <summary>board: the beat types this base can build. Empty = every type the beats kind knows.</summary>
            public List<string> allows { get; set; } = new();
            /// <summary>
            /// board: with no beatSlots, the ref alias each beat's marker is copied from. Removed after the build, once
            /// nothing names it (questScript's PrimaryRef is repointed at beat 1's marker first). -1 = beatSlots[0]'s.
            /// </summary>
            public int markerSource { get; set; } = -1;
            /// <summary>board: the removed driver's own aliases, named by nothing once it goes, removed with it.</summary>
            public List<int> driverAliases { get; set; } = new();
            /// <summary>
            /// board: a quest in the same mod whose aliases are COPIED IN, renumbered (borrowAliases), because the base has
            /// none of its own: the player (the arrival hook and a recover measure to him), the object slots a talk, pickup,
            /// use or deliver needs, and a second place. Every alias link inside a copy is remapped through the list, and the
            /// source's main place (borrowPlaceAlias) becomes this template's placeAlias; any other link is refused.
            /// </summary>
            public string? borrowFrom { get; set; }
            public List<BorrowAlias> borrowAliases { get; set; } = new();
            public int borrowPlaceAlias { get; set; } = -1;
            /// <summary>board: the MiscItem a pickup's item is cloned from when the removed driver carries no CargoObject.</summary>
            public string? itemSource { get; set; }
            /// <summary>
            /// The second place's PLANET, CREATED when a beat is at the second place: a copy of the base's planet alias whose
            /// parent is the second place, so &lt;SecondPlanet&gt; names where he flies. Null = this template has none.
            /// </summary>
            public SecondPlanet? secondPlanet { get; set; }
            /// <summary>
            /// board: the stage set when the player first comes near step one's marker, after the accept. The broadcast plays
            /// there, not on the accept: his 2026-10-14, "You shouldn't recieve radio messages just after picking up a
            /// missionboard mission. It's a posting someone has made." -1 = the broadcast plays on entryStage.
            /// </summary>
            public int arrivalStage { get; set; } = -1;
            public List<string> cannot { get; set; } = new();
            public string verifiedFrom { get; set; } = "";
        }
        /// <summary>The two kinds: a slate Delve ("beats") and a mission-board one ("board") on the same machinery.</summary>
        private static readonly string[] Kinds = { "beats", "board" };
        private static bool IsBoard(Template t) => t.kind == "board";
        /// <summary>
        /// The stage a place's people and occupants (an ending's person, a who) are numbered AFTER, each on its own stage below
        /// the first beat's (entryStage + 10): the approach's on a slate Delve (5, so 6, 7...), the arrival's on a board (15,
        /// so 16 to 19), which has no approach because nothing of a board mission may happen before the accept (2026-10-15,
        /// duo_delve12a, his "teach board who"). -1 = neither, so the template places nobody.
        /// </summary>
        private static int OccupantBase(Template t) => t.approachStage >= 0 ? t.approachStage : t.arrivalStage;
        private sealed class BorrowAlias { public int from { get; set; } public int to { get; set; } }
        private sealed class SecondPlanet { public int cloneOf { get; set; } public int id { get; set; } public string name { get; set; } = ""; }
        private sealed class BeatSlot
        {
            public int markerAlias { get; set; }
            public int activatorAlias { get; set; }
            public int objective { get; set; }
            public int journalStage { get; set; }
            /// <summary>
            /// Set by the build, never by the registry: the alias this beat's objective points at when it
            /// is neither its activator nor its marker (beat 3's carrier, filled at runtime).
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore] public int targetAlias { get; set; } = -1;
            public int ObjectiveTarget => targetAlias >= 0 ? targetAlias : activatorAlias >= 0 ? activatorAlias : markerAlias;
            /// <summary>
            /// Set by the build: a SECOND objective target, the place itself, for a beat whose own target does not exist
            /// until the player is near (a clear's enemies, spawned then). Without it the objective has nothing to point
            /// at and the site is not on his map until he is already there.
            /// </summary>
            [System.Text.Json.Serialization.JsonIgnore] public int siteAlias { get; set; } = -1;
        }
        /// <summary>
        /// A created place. cloneOf names the base location alias it is copied from, so it inherits
        /// that alias's flags and its GetDistance leash (the one condition type this tool preserves
        /// rather than constructs); its marker and theme conditions are then written from the recipe
        /// like any other place.
        /// </summary>
        private sealed class ThirdPlace { public int cloneOf { get; set; } = -1; public string name { get; set; } = ""; }
        /// <summary>orbitFrom's four aliases, by their SOURCE ids: the orbit location, the space-cell collection created at it,
        /// a marker filled from that collection, and the map marker filled from it.</summary>
        private sealed class OrbitAliases
        {
            public int place { get; set; } = -1;
            public int cell { get; set; } = -1;
            public int marker { get; set; } = -1;
            public int mapMarker { get; set; } = -1;
            /// <summary>Every source id the row names; -1 (absent) is skipped.</summary>
            public int[] All => new[] { place, cell, marker, mapMarker }.Where(x => x >= 0).ToArray();
        }
        /// <summary>
        /// A template's PARTS file: the aliases its build copies off other quests, as gen_questmodel models, each beside the
        /// quest it was read from (provenance, never read at build). Written by `gen_delve shell` and owned after, exactly as
        /// the shell is. A part the template does not take stays null.
        /// </summary>
        private sealed class Parts
        {
            public int schema { get; set; } = 1;
            /// <summary>remotePlaceFrom's main place alias (a PCM request), which a remote place is copied from.</summary>
            public string? remotePlaceFrom { get; set; }
            public QAlias? remotePlace { get; set; }
            /// <summary>borrowFrom's aliases, one per borrowAliases row, by their SOURCE ids (the build renumbers them).</summary>
            public string? borrowFrom { get; set; }
            public List<QAlias> borrowed { get; set; } = new();
            /// <summary>Vanilla's empty wave collection (UC08_AB_AnomalyBattle's ActiveHostiles), which hold and clear copy.</summary>
            public string? wavesFrom { get; set; }
            public QAlias? waves { get; set; }
            /// <summary>orbitFrom's four aliases (OrbitAliases), by their SOURCE ids, which a place.third.orbit copies in.</summary>
            public string? orbitFrom { get; set; }
            public List<QAlias> orbit { get; set; } = new();
        }
        /// <summary>The vanilla quest a hold's and a clear's wave collections are copied from, and how its alias is found.</summary>
        private const uint WaveQuestId = 0x03E832;
        private const string WaveMemberName = "ActiveHostiles";
        private static IQuestCollectionAliasGetter? WaveCollectionOf(IQuestGetter? q) =>
            q?.Aliases.OfType<IQuestCollectionAliasGetter>()
             .FirstOrDefault(c => c.Collection.Count == 1 && c.Collection[0].ReferenceAlias?.Name == WaveMemberName);
        /// <summary>A template whose recipes may hold or clear copies the wave collection (allows empty = every type).</summary>
        private static bool TakesWaves(Template t) => t.allows.Count == 0 || t.allows.Contains("hold") || t.allows.Contains("clear");
        /// <summary>Does this template's build take anything off another quest?</summary>
        /// <summary>A recipe whose third place is an orbit (Place.orbit).</summary>
        private static bool IsOrbit(Recipe r) => r.place.third?.orbit != null;
        /// <summary>place.third.orbit's scopes: the location keyword the orbit must share with the main place.</summary>
        private static readonly Dictionary<string, FormKey> OrbitScopes = new()
        {
            ["planet"] = FormKey.Factory("070A54:Starfield.esm"),   // LocTypeMajorOrbital: a planet or a moon
            ["system"] = FormKey.Factory("00149F:Starfield.esm"),   // LocTypeStarSystem
            // "near": no shared location at all. MB01's OWN conditions, verbatim (BodyHasKeyword LocTypeMajorOrbital, not
            // LocTypeSENotAllowed, GetDistanceGalacticParsec from the player), the parsec limit from place.third.parsecs. His
            // 2026-10-22 parsec idea for the dungeon, pointed at the orbit after "planet" and "system" never started.
            ["near"] = FormKey.Null,
        };
        /// <summary>The beat types built in orbit: a thing used from the ship.</summary>
        private static readonly string[] OrbitBeatTypes = { "use", "pickup", "deliver" };
        /// <summary>The marker an orbit beat stands at, filled from the space cell: his MB01's (orbitFrom's marker alias).</summary>
        private const string OrbitMarker = "se_TravelMarkerLocRefB02";
        private static bool TakesParts(Template t) => t.remotePlaceFrom != null || t.borrowFrom != null || TakesWaves(t) || t.orbitFrom != null;
        private sealed class TemplateFile { public int schema { get; set; } public List<Template> templates { get; set; } = new(); }

        private sealed class Recipe
        {
            public int schema { get; set; }
            public string id { get; set; } = "";
            public string template { get; set; } = "";
            public string author { get; set; } = "";
            public string source { get; set; } = "";
            public Place place { get; set; } = new();
            public Prose prose { get; set; } = new();
            public List<Beat> beats { get; set; } = new();
            /// <summary>OPTIONAL: the surprise between the find and the choose endings (journal, and a box or a spoken line).</summary>
            public Offer? offer { get; set; }
            /// <summary>
            /// beats, REQUIRED when the last step is a group (and refused otherwise): the completing stage's
            /// journal line. The group's last member sets its own stage and then the completing stage in the
            /// same instant, and the quest log shows only the newest line, so without this the player reads
            /// the BASE's recap. Ending on a single beat, that beat's journal already IS the completing line.
            /// A new optional field, so schema stays 1. His play of duo_delve07, 2026-10-08.
            /// </summary>
            public string? recap { get; set; }
            /// <summary>
            /// beats, OPTIONAL when the last step is a group (and refused otherwise): the speaker's closing line, spoken on the
            /// completing stage, recap's spoken twin. A group's members fire in any order, so no member's say can be the last
            /// thing heard (Jessica, remote03a); a single-beat ending's own say already is. His play of remote03a, 2026-10-14:
            /// "first radio message played but nothing at the end?" A new optional field, so schema stays 1.
            /// </summary>
            public List<string>? recapSay { get; set; }
            /// <summary>
            /// beats, OPTIONAL: what happens the FIRST time the player comes within range of one beat's object
            /// (the delivery point). A stock DefaultAliasOnDistanceLessThan on the player sets the template's
            /// approach stage, whose fragment does the rest. Ported from duo_delve_driver.OnDistanceLessThan.
            /// </summary>
            public Approach? approach { get; set; }
            /// <summary>
            /// beats, OPTIONAL (required by broadcast or any beat's say): WHO speaks this Delve's lines. Spoken over
            /// the radio, with no actor in the world: his ask 2026-10-09, "a broadcast to the player at the start
            /// of the mission", then "something different, speech ... there should be like topics that play on
            /// stages in the vanilla game".
            /// </summary>
            public Speaker? speaker { get; set; }
            /// <summary>beats, OPTIONAL: a line the speaker says when the quest starts (stage 0). Plain words, no tokens.</summary>
            public List<string>? broadcast { get; set; }
            /// <summary>
            /// OPTIONAL, either kind: the EditorID of a Story Manager quest node in the template's own mod that deals this
            /// Delve, i.e. a slate pool (duo_MS_ArtifactMap_LocalNode and its siblings). The build appends the quest to
            /// that node and removes the previous build's entry from every node. Absent = in no pool, started by hand with
            /// `startquest`, as every Delve was before this field. His go, 2026-10-11: "do you have the tooling to add the
            /// delves to the existing story nodes" / "yeah go". A new optional field, so schema stays 1.
            /// </summary>
            public string? deal { get; set; }
        }
        /// <summary>The voice of a Delve's spoken lines.</summary>
        private sealed class Speaker
        {
            /// <summary>The subtitle's speaker name ("Relay Control"). No tokens.</summary>
            public string? name { get; set; }
            /// <summary>A vanilla VoiceType EditorID: the folder the audio lives in (GenericMale01, GenericFemale01, as Outlaws 02 ships).</summary>
            public string? voice { get; set; }
            /// <summary>The ElevenLabs voice id the audio is generated in. His pick for the first one: CwhRBWXzGAHq8TQ4Fs17.</summary>
            public string? elevenlabs { get; set; }
        }
        private sealed class Approach
        {
            /// <summary>The beat (1-based) whose object is approached; it must have an object of its own.</summary>
            public int to { get; set; }
            /// <summary>Place 1 to 5 people from the base's own civilian list around that object.</summary>
            public bool civilians { get; set; }
            /// <summary>OPTIONAL: a pickup beat (1-based) at the same place whose crate is moved out past the site's edge, if not yet taken.</summary>
            public int? lose { get; set; }
        }
        /// <summary>A named NPC cloned from a vanilla template by EditorID (NPCTools' friendly set is the menu).</summary>
        private sealed class Person
        {
            public string? name { get; set; }
            public string? template { get; set; }
            /// <summary>OPTIONAL: an Outfit EditorID written as the NPC's DefaultOutfit. Absent = the template's own.</summary>
            public string? outfit { get; set; }
            /// <summary>OPTIONAL: nameless friendly NPCs placed around this person. Absent = they stand alone.</summary>
            public Company? company { get; set; }
            /// <summary>talk, REQUIRED there: the VoiceType EditorID his lines are filed under (GenericMale01 / GenericFemale01).</summary>
            public string? voice { get; set; }
            /// <summary>talk, REQUIRED there: the ElevenLabs voice id his lines are generated in (docs/quests/voices.md).</summary>
            public string? elevenlabs { get; set; }
            /// <summary>
            /// talk, OPTIONAL: what he says as the player WALKS UP, before any conversation (his 2026-10-19: "lines the npc say
            /// before a conversation when the player walks up", and Jessica writes them). One is picked each time, voiced in
            /// his own voice, built as vanilla's Hello shape (MQ101's Lin: a group gated on GetIsID, the lines under it).
            /// Absent, he says nothing of his own, and vanilla's ambient greetings no longer match him either (see Factions).
            /// </summary>
            public List<List<string>>? hellos { get; set; }
        }
        /// <summary>talk: one question the player can ask and the person's answer. Prompt is silent menu text, reply is voiced.</summary>
        private sealed class Exchange
        {
            public string? prompt { get; set; }
            public List<string>? reply { get; set; }
            /// <summary>talk, optional: a vanilla perk's EditorID (BackgroundDiplomat, Skill_Medicine, TRAIT_Empath,
            /// FactionCrimsonFleetPerk...). Set on the PLAYER's line as vanilla's own INFO.Perk field, which is how the game
            /// tags the choice [Diplomat] and offers it only to a player with the perk (manual hub Inbox, 2026-10-14: 1,027
            /// vanilla lines, mostly with no condition). Never on exchanges[0]: a perk-less player could not finish.</summary>
            public string? perk { get; set; }
        }
        /// <summary>A FormList of actor bases (Overtime's duo_GangMembersList_Civ_* are the friendly ones) and a count range.</summary>
        private sealed class Company { public string? list { get; set; } public int min { get; set; } public int max { get; set; } }
        /// <summary>
        /// The offer: the journal line at the find, and EITHER a message box OR a spoken radio line (say), never both
        /// (his "either or", 2026-10-11). say is beats-only, needs the recipe's speaker, and plays on the stage that
        /// opens the choice AFTER the find's box is closed (Message.Show waits), so the two never overlap. His ask,
        /// 2026-10-12 on 06b: "the call from the guy to buy is a textbox and not a radio messsage".
        /// </summary>
        private sealed class Offer { public string? journal { get; set; } public BeatMessage? message { get; set; } public List<string>? say { get; set; } }
        private sealed class Place
        {
            public Theme theme { get; set; } = new();
            public string? leash { get; set; }
            /// <summary>
            /// OPTIONAL, written: the Global every OTHER place is held within, of the main place and of each other
            /// (LeashPlaces). Absent = duo_longdrivedistance (7,500). His 2026-10-13, 08a: "tighten this one quest
            /// down to duo_shortdrivedistance". The main place's own limit (from the player) is not this field.
            /// </summary>
            public string? reach { get; set; }
            /// <summary>The second POI's own theme, for beats with place "second". Absent = no theme.</summary>
            public Place? second { get; set; }
            /// <summary>The third POI's own theme, for beats with place "third". Absent = no theme.</summary>
            public Place? third { get; set; }
            /// <summary>RETIRED with the delve4 kind, 2026-10-09: kept only so the lint can refuse it (a beats Delve writes approach.civilians).</summary>
            public bool? civilians { get; set; }
            /// <summary>
            /// OPTIONAL, main place only: true = the place is REQUESTED (a PCM request, PCM_QuestGeneral, the template's
            /// remotePlaceFrom alias copied verbatim), so the planet content manager places a NEW site on another planet with
            /// its own landing marker, rather than the mission finding one near the player. Every beat is at that one place
            /// (his 2026-10-14 pick, "a": no driving). Its pool is gen_delve pcmpool's, not the POI census.
            /// </summary>
            public bool? remote { get; set; }
            /// <summary>
            /// OPTIONAL, a REQUESTED place only (remote: true): the request's GetDistanceGalacticParsec limit from the player
            /// becomes this number, in place of the copied duo_distanceformission Global. His 2026-10-22, 19a: "make the Parsec
            /// distance like 1. That should be same system at least". The condition's function and subject are kept; only its
            /// comparison value changes, from a Global to this float.
            /// </summary>
            public double? parsecs { get; set; }
            /// <summary>
            /// OPTIONAL, a REQUESTED place only: false = the request's own planet condition (its ALPS conditions; on Overtime's
            /// remote base, IsTrueForConditionForm PCM_CND_BlockCreation_Habitability02_Medium) is dropped, so the request is
            /// not limited to medium-habitability planets. His 2026-10-22, 19a: "remove the
            /// PCM_CND_BlockCreation_Habitability02_Medium check". true is refused: leave it out to keep the condition.
            /// </summary>
            public bool? habitability { get; set; }
            /// <summary>
            /// place.third only: the third place is an ORBIT, over the main place's planet, not a POI (his 2026-10-22, "go on
            /// 19a, try over"). The template's orbitFrom chain is copied in: a LocTypeOrbit location tested
            /// IsInSameCurrentLocationAsAlias(the main place, LocTypeMajorOrbital), a levelled space cell created at it, and
            /// each beat's marker filled from inside that cell by its `at` ref type. Its beats are things used from the ship
            /// (use, pickup, deliver), as his MB01 satellite is. No theme, no who, no leash: an orbit is not a drawn POI.
            /// "planet" = the orbit over the main place's planet (LocTypeMajorOrbital); "system" = any orbit in the main
            /// place's star system (LocTypeStarSystem), his "yeah whole system" after "planet" would not start (2026-10-22).
            /// </summary>
            public string? orbit { get; set; }
            // ⛔ There is no "planet" key, and an unknown key is refused. A second request lands where the planet content
            // manager puts it, the first site's planet or another. Two planet guards on a place (10a 2026-10-14, 12a
            // 2026-10-19) each CRASHED the game on start, and his ruling 2026-10-19 is "Stop doing it", "it's fine if the
            // mission is on the same planet". So no place carries one.
            /// <summary>
            /// OPTIONAL: who is at THIS place (his dial, 2026-10-11, office/projects/delves/backlog.md "Who's there").
            /// Absent = empty, the default. infested, workers and spacers are built; any other kind is refused by
            /// name, never read as empty, because a briefing that names a crew at an empty site is the failure this exists for.
            /// </summary>
            public Who? who { get; set; }
        }
        /// <summary>
        /// Who is at a place, placed round one object there when the player comes within 250 m of it.
        /// infested: ONE species drawn from list (creature bases; Overtime's duo_GangMembersList_PreditorMegalist_*),
        /// set to attack. workers: a friendly MIX from list (the duo_GangMembersList_Civ_* lists). spacers: a hostile
        /// MIX from list (a gang list). "the dead" is designed and refused (delves backlog, who's there).
        /// beat: OPTIONAL, 1-based, which pickup / use / deliver at this place they gather round; absent = the
        /// first one (his 2026-10-13: crowds driven from the json).
        /// </summary>
        private sealed class Who { public string? kind { get; set; } public string? list { get; set; } public int min { get; set; } public int max { get; set; } public int? beat { get; set; } }
        private static readonly string[] WhoKinds = { "infested", "workers", "spacers" };
        private sealed class Theme { public List<string> require { get; set; } = new(); public List<string> exclude { get; set; } = new(); }
        /// <summary>card: board only, REQUIRED there, the mission board's description (the card he reads before taking the job).</summary>
        private sealed class Prose { public string? name { get; set; } public string? briefing { get; set; } public string? card { get; set; } }
        private sealed class Beat
        {
            public string at { get; set; } = "";
            public string? objective { get; set; }
            public string? journal { get; set; }
            /// <summary>"main" (default), "second" or "third": which drawn POI this beat happens at.</summary>
            public string? place { get; set; }
            /// <summary>0 main, 1 second, 2 third; -1 for a name that is none of them (the lint refuses it).</summary>
            public int PlaceIndex => place == null || place.Equals("main", StringComparison.OrdinalIgnoreCase) ? 0
                                   : place.Equals("second", StringComparison.OrdinalIgnoreCase) ? 1
                                   : place.Equals("third", StringComparison.OrdinalIgnoreCase) ? 2 : -1;
            public bool Second => PlaceIndex == 1;
            /// <summary>
            /// OPTIONAL: a pausing message box shown when this beat fires. Absent = no box.
            /// A new optional field, so schema stays 1 and every recipe written before it builds unchanged.
            /// </summary>
            public BeatMessage? message { get; set; }
            /// <summary>beats: "use", "pickup" or "deliver". Absent on every other kind.</summary>
            public string? type { get; set; }
            /// <summary>
            /// OPTIONAL, and REQUIRED on an object beat whose marker is a travel ring: why the thing may sit at the POI's
            /// edge. His rule, 2026-10-14: "Items that are important to someone shouldn't be on travel ring. Infrastructure,
            /// Large cargo and hostiles can be on the travel ring". "infrastructure" or "large-cargo"; fights and recovers
            /// (a thief holds the thing) are the hostiles and need no declaration. Anything important to someone goes on
            /// REMarkerMediumLocRef instead, or REMarkerSmallImportantLocRef for a second, small one at the same place.
            /// </summary>
            public string? ring { get; set; }
            /// <summary>beats: consecutive use beats sharing a group name are done in ANY order, with one counted objective.</summary>
            public string? group { get; set; }
            /// <summary>beats, pickup only: the inventory name of what this beat hands the player (a cloned item).</summary>
            public string? item { get; set; }
            /// <summary>beats, OPTIONAL: the NIF this beat's object wears, and its activate prompt. Absent = the base's.</summary>
            public string? model { get; set; }
            public string? name { get; set; }
            /// <summary>
            /// beats, OPTIONAL: the marker is a PLACED OBJECT (REContainerLocRef is the box itself), so disable
            /// it when the quest starts and let this beat's object stand in its place. His way in his own
            /// quests (ccs_missioninfestation01: ContainerRef.Disable(False), never re-enabled). duo_delve08,
            /// 2026-10-09: the locker spawned INSIDE the box. Absent = false.
            /// </summary>
            public bool replace { get; set; }
            /// <summary>
            /// beats, OPTIONAL: this beat is an EARLIER beat's object visited again (1-based beat number), not a
            /// new one: the centre in 03 to 05 is visited to find the half gone and again to finish. It names
            /// the same at and place as its target, and only a use beat can be returned to, once.
            /// </summary>
            public int? returnTo { get; set; }
            /// <summary>
            /// beats, a recover, OPTIONAL: [min, max] distance from the beat's marker at which the holder is placed, out on
            /// open ground at a random angle, his gang round him (his idea and his 05a note, 2026-10-19: "the spacers spawn in
            /// the site"). Route A of duo_offpoiprobe (spike 2026-10-19-objectives-off-the-poi). Absent = at the marker.
            /// </summary>
            public List<int>? outside { get; set; }
            /// <summary>
            /// beats, OPTIONAL: consecutive deliver beats sharing a choose name are the ENDINGS (the last step):
            /// each is shown, walking to one completes the quest on its own stage and hides the others (hidden,
            /// never failed: neither side is the villain). Jessica's Type 2; his ruling that the choice is made
            /// by WHERE YOU GO. The first ending completes on 100, the next on a copy of 100 at 110, and so on.
            /// </summary>
            public string? choose { get; set; }
            /// <summary>beats, a choose ending: Overtime's pay tier (easy / med / hard) written onto its completing stage.</summary>
            public string? reward { get; set; }
            /// <summary>beats, a choose ending, OPTIONAL: the named NPC (and company) placed beside it on the first approach after the find.</summary>
            public Person? person { get; set; }
            /// <summary>
            /// beats, a hold: how many waves come, one after another, each when the last is down (Jessica's
            /// Type 6, his "a hold the point while enemies attack in waves misson", 2026-10-08).
            /// </summary>
            public int? waves { get; set; }
            /// <summary>beats, a hold: each wave's head count, [min, max], rolled per wave. Both at least 1: an empty wave never clears.</summary>
            public List<int>? size { get; set; }
            /// <summary>beats, a hold, OPTIONAL: the earlier beat (1-based) whose object is defended, which the objective points at. Absent = the spawn marker.</summary>
            public int? defend { get; set; }
            /// <summary>
            /// beats, a hold, OPTIONAL: seconds a wave may stay alive before the quest moves on without it
            /// (the stuck-enemy guard: his infestation driver's 180 s pity timer). Absent = 180.
            /// </summary>
            public int? stuck { get; set; }
            /// <summary>
            /// beats, a hold, OPTIONAL: seconds after a wave spawns before its enemies become objective markers, so
            /// a wave he has not found is pointed out (his ask 2026-10-12: "after like 20 seconds of the wave we set
            /// the enemies as objectives? That way if the player hasn't seen them it points them out"). Absent = 20;
            /// 0 = never. Each wave is its own objective (hold objective + 1 + wave), hidden when the wave is down,
            /// so a cleared wave's bodies never keep a marker.
            /// </summary>
            public int? reveal { get; set; }
            /// <summary>beats, a hold, OPTIONAL: the revealed waves' objective text. Absent = "Eliminate the attackers" (Eliza's placeholder).</summary>
            public string? revealObjective { get; set; }
            /// <summary>
            /// beats, a hold, OPTIONAL: each wave's style, one per wave, "squad" or "horde". A squad comes as one
            /// group from anywhere but in front of him; a horde is split between his left and right, so it closes from two sides
            /// (his asks 2026-10-10: "scattering them across points / grouping for horde vs squad combat styles";
            /// close behind him since 2026-10-12, "spawning the enemies closer to the player").
            /// Absent = every wave a squad, which is how every hold played before the field existed.
            /// </summary>
            public List<string>? style { get; set; }
            /// <summary>beats, OPTIONAL: a line the recipe's speaker says over the radio when this beat's stage is set. Plain words, no tokens.</summary>
            public List<string>? say { get; set; }
            /// <summary>talk, REQUIRED there: what the person says when the player first speaks to him. Voiced, no tokens.</summary>
            public List<string>? greeting { get; set; }
            /// <summary>
            /// talk, REQUIRED there: the questions, in menu order. exchanges[0] ENDS the conversation and sets the beat's
            /// stage when its reply finishes; the rest can be asked in any order and loop back to the menu
            /// (docs/specs/npc_dialogue.md, the Completion and Regular topic shapes).
            /// </summary>
            public List<Exchange>? exchanges { get; set; }
            /// <summary>
            /// defeat, REQUIRED there: the actor base (Npc or LeveledNpc, own mod or a master) who appears at `at` when the
            /// step begins; the step is done when he dies (the Bounty driver's boss, spike 2026-10-14-defeat-and-clear-beats).
            /// </summary>
            public string? target { get; set; }
            /// <summary>defeat, OPTIONAL: who stands with the target, a mix drawn from list. They need not die.</summary>
            public Crowd? gang { get; set; }
            /// <summary>clear, REQUIRED there: who appears at `at` when the step begins; the step is done when every one is dead (the Hunt driver).</summary>
            public Crowd? enemies { get; set; }
            /// <summary>defeat and clear, OPTIONAL: each enemy carries one thing from list, each with chance percent (the drivers: artifacts, 25).</summary>
            public Loot? loot { get; set; }
            /// <summary>
            /// defeat and clear, OPTIONAL: a FRIENDLY team that arrives BY SHIP when the player comes near (his 2026-10-21, "build a
            /// new delve mission with the landings", after the strike team test on qst11a). If the place has its own landing pad
            /// near its `land` marker, the ship is placed on it and comes down from orbit by itself (his fact, the landed bounties
            /// qst12a to qst16d); on landing its ramps open and the team comes out of its load door, then it takes off. With no
            /// pad there is no ship and the team arrives on foot (his "Pad or Squad", 2026-10-21). papyrus/duo_delve_landing.psc.
            /// </summary>
            public Support? support { get; set; }
        }
        /// <summary>
        /// A team who arrives by ship: min..max drawn from list, landed by ship (a leveled ship list such as
        /// LvlShip_UCSecurity_Transport) on the place's land marker, when the player comes within near (flat; absent = 300).
        /// </summary>
        private sealed class Support
        {
            public string? list { get; set; }
            public int min { get; set; }
            public int max { get; set; }
            public string? ship { get; set; }
            public string? land { get; set; }
            public int? near { get; set; }
        }
        /// <summary>A crowd drawn from a FormList: min..max of them; species "one" = a single entry for all (the Hunt driver), "mix" = drawn per enemy.</summary>
        private sealed class Crowd { public string? list { get; set; } public int min { get; set; } public int max { get; set; } public string? species { get; set; } public Pods? pods { get; set; } }
        /// <summary>A clear's enemies arriving by DROP POD (2026-10-21, his "let's do 3 per pod and 3 pods in different points to
        /// start with"): count pods land round the spawn marker and per enemies step out of each, so the pods set the number
        /// and min/max are refused beside them. All vanilla, no record of ours (manual 32, "how a DROP POD comes down").</summary>
        private sealed class Pods { public int count { get; set; } public int per { get; set; } }
        private sealed class Loot { public string? list { get; set; } public int chance { get; set; } }
        private sealed class BeatMessage { public string? title { get; set; } public string? text { get; set; } }

        private static readonly JsonSerializerOptions JsonOpts = new()
        { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };

        private sealed record Issue(bool Fatal, string Text);

        // ------------------------------------------------------------------ entry

        public static int Run(string[] args)
        {
            string verb = args.Length > 1 ? args[1].ToLowerInvariant() : "";
            bool dry = args.Any(a => a.Equals("--dry", StringComparison.OrdinalIgnoreCase));
            bool spread = args.Any(a => a.Equals("--spread", StringComparison.OrdinalIgnoreCase));
            string? path = args.Skip(2).FirstOrDefault(a => !a.StartsWith("--"));
            // Before the data folder and the environment: it needs neither (gen_delve_selftest.cs).
            if (verb == "selftest") return SelfTest();

            string? dataDir = ResolveDataDir();
            if (dataDir == null)
            {
                Console.WriteLine("REFUSED: could not find data/delves. Tried, from the assembly and the working directory:");
                foreach (var c in CandidateRoots()) Console.WriteLine("  " + Path.Combine(c, "data", "delves"));
                return 1;
            }
            Console.WriteLine("  data: " + dataDir);

            var templates = LoadTemplates(dataDir, out string? terr);
            if (templates == null) { Console.WriteLine("REFUSED: " + terr); return 1; }

            switch (verb)
            {
                case "templates": return ListTemplates(templates);
                // the census verbs: commands/quest/delve_census.cs
                case "census": return DelveCensus.Census(args.Any(a => a.Equals("--vanilla", StringComparison.OrdinalIgnoreCase)));
                case "spread": return DelveCensus.Spread(args.Skip(2).Where(a => !a.StartsWith("--")).ToList(),
                                             args.Any(a => a.Equals("--dungeons", StringComparison.OrdinalIgnoreCase)));
                case "markers": return DelveCensus.Markers(args.Any(a => a.Equals("--dungeons", StringComparison.OrdinalIgnoreCase)));
                case "bases": return DelveCensus.MarkerBases(args.Skip(2).Where(a => !a.StartsWith("--")).ToList());
                case "footprint": return DelveCensus.Footprint();
                case "keywords": return DelveCensus.PoolKeywords(args.Skip(2).Where(a => !a.StartsWith("--")).ToList());
                case "pcmpool": return DelveCensus.PcmPool(args.Skip(2).Where(a => !a.StartsWith("--")).ToList());
                case "models": return ModelCheck(dataDir, templates, args.Skip(2).Where(a => !a.StartsWith("--")).ToList());
                case "lint":
                    if (args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase))) return LintAll(dataDir, templates);
                    return WithRecipe(path, dataDir, templates, (r, t, env) => Grade(r, t, env, null, spread) ? 0 : 1);
                case "build":
                {
                    int ei = Array.FindIndex(args, a => a.Equals("--emit", StringComparison.OrdinalIgnoreCase));
                    var opts = new BuildOptions(
                        FromBase: args.Any(a => a.Equals("--from-base", StringComparison.OrdinalIgnoreCase)),
                        EmitDir: ei >= 0 && ei + 1 < args.Length ? args[ei + 1] : null,
                        DataDir: dataDir,
                        Spread: spread);
                    if (opts.EmitDir != null && !dry) { Console.WriteLine("REFUSED: --emit is a dry-run output; add --dry."); return 1; }
                    return WithRecipe(path, dataDir, templates, (r, t, env) => Build(r, t, env, dry, opts));
                }
                case "shell": return MakeShell(path, dataDir, templates);
                case "unbuild": return Unbuild(path, templates, dry);
                case "testnode": return TestNode(args, templates, dry);
                case "nodeflag": return NodeFlag(args, templates, dry);
                // the legacy mission system: commands/quest/delve_legacy.cs
                case "legacy": return DelveLegacy.Run(args, dataDir, templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), dry);
                // the rare tier: commands/quest/delve_rare.cs
                case "rare": return DelveRare.Run(args, dataDir, templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), dry);
                // a built Delve changes pool by a list edit, never a rebuild: commands/quest/delve_move.cs
                case "move": return DelveMove.Run(args, dataDir, templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList(), dry);
                default:
                    Console.WriteLine("Usage: gen_delve templates");
                    Console.WriteLine("       gen_delve lint  <recipe.json | recipeId> [--spread]   --spread adds the beat-separation figures (~30 s more)");
                    Console.WriteLine("       gen_delve lint  --all          every recipe, one game-data load, one line each");
                    Console.WriteLine("       gen_delve models [recipe ...]   the final test: every beat model resolves (one ~36 s load-order walk)");
                    Console.WriteLine("       gen_delve build <recipe.json | recipeId> [--dry]");
                    Console.WriteLine("       gen_delve build <recipe> --dry --emit <dir>   write the would-be plugin and the emitted model to <dir>, never the live plugin");
                    Console.WriteLine("       gen_delve build <recipe> --from-base   start from the base quest's clone, not the template's shell (the A/B proof)");
                    Console.WriteLine("       gen_delve shell <templateId>   write the template's shell model from its base quest, once");
                    Console.WriteLine("       gen_delve unbuild <builtId> [--dry]   take a built Delve's records back out of the plugin");
                    Console.WriteLine("       gen_delve testnode on|off <testNode> <realNode> [--keep q,q | --promote] [--dry]   swap a slate pool's parking condition");
                    Console.WriteLine("       gen_delve nodeflag <node> runone on|off [--dry]   one quest per event (NumQuestsToRun, max 1)");
                    Console.WriteLine("       gen_delve legacy <pool node> [--dry]   move the pool's non-Delve quests to <pool>_Legacy, behind the Classic Missions setting");
                    Console.WriteLine("       gen_delve rare <pool node> <delveId>[,...] [--dry]   move built Delves into <pool>_Rare, tried on duo_RareDelveChance% of the pool's events");
                    Console.WriteLine("       gen_delve move <delveId> <fromNode> <toNode> [--dry]   a built Delve changes pool (a list edit, never a rebuild)");
                    Console.WriteLine("       gen_delve pcmpool <pcmBranchNode> [refType ...]   what a PCM request under that node can place, and which carry the ref types");
                    Console.WriteLine("       gen_delve selftest   the build's pure helpers, no plugin or game data loaded, exit 1 on failure");
                    return 1;
            }
        }

        // ------------------------------------------------------------------ nodeflag

        /// <summary>
        /// `gen_delve nodeflag <node> runone on|off [--dry]`: set or clear a slate pool's "start one quest per event"
        /// (QuestFlags.NumQuestsToRun with MaxNumQuestsToRun = 1, the shape his board test node
        /// _duo_MB_MissionNode_test already carries). His ask 2026-10-12: one dataslate should give one mission,
        /// falling through to another quest when a quest's aliases fail; today a pool without the flag started
        /// several Delves off one slate. ⚠ WHAT THE FLAG MEANS IN STARFIELD IS THE EXPERIMENT, not a fact: per
        /// event (one quest per slate, and a quest that fails to fill should not count), or a cap on how many of
        /// the node's quests run at once (his reading). The record also carries a separate MaxConcurrentQuests,
        /// which is suggestive only. The test: two slates picked up without finishing the first.
        /// </summary>
        private static int NodeFlag(string[] args, List<Template> templates, bool dry)
        {
            var pos = args.Skip(2).Where(a => !a.StartsWith("--")).ToList();
            if (pos.Count != 3 || pos[1] != "runone" || (pos[2] != "on" && pos[2] != "off"))
            { Console.WriteLine("REFUSED: gen_delve nodeflag <node> runone on|off [--dry]"); return 1; }
            string nodeName = pos[0]; bool on = pos[2] == "on";
            var mods = templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; nodeflag knows which plugin only when they agree."); return 1; }
            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            var node = myMod.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, nodeName, StringComparison.OrdinalIgnoreCase));
            if (node == null) { Console.WriteLine($"REFUSED: no Story Manager quest node '{nodeName}' in {mods[0]}.esm."); return 1; }

            bool had = node.QuestFlags.HasFlag(StoryManagerQuestNode.QuestFlag.NumQuestsToRun);
            Console.WriteLine();
            Console.WriteLine($"=== NODEFLAG {node.EditorID} runone {pos[2]} ===");
            Console.WriteLine($"  before: QuestFlags = {node.QuestFlags}, MaxNumQuestsToRun = {node.MaxNumQuestsToRun?.ToString() ?? "(none)"}, MaxConcurrentQuests = {node.MaxConcurrentQuests?.ToString() ?? "(none)"}");
            if (had == on && (!on || node.MaxNumQuestsToRun == 1)) { Console.WriteLine("REFUSED: already " + pos[2] + ". Nothing written."); return 1; }
            if (on) { node.QuestFlags |= StoryManagerQuestNode.QuestFlag.NumQuestsToRun; node.MaxNumQuestsToRun = 1; }
            else { node.QuestFlags &= ~StoryManagerQuestNode.QuestFlag.NumQuestsToRun; node.MaxNumQuestsToRun = null; }
            Console.WriteLine($"  after : QuestFlags = {node.QuestFlags}, MaxNumQuestsToRun = {node.MaxNumQuestsToRun?.ToString() ?? "(none)"}");
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath);
            using var reread = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            var n2 = reread.StoryManagerQuestNodes.First(n => n.FormKey == node.FormKey);
            bool ok = n2.QuestFlags.HasFlag(StoryManagerQuestNode.QuestFlag.NumQuestsToRun) == on && (on ? n2.MaxNumQuestsToRun == 1 : n2.MaxNumQuestsToRun == null);
            var mastersAfter = string.Join(", ", reread.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] off disk: QuestFlags = {n2.QuestFlags}, MaxNumQuestsToRun = {n2.MaxNumQuestsToRun?.ToString() ?? "(none)"}");
            Console.WriteLine($"  [{(mastersAfter == mastersBefore ? "OK  " : "FAIL")}] the plugin's masters are unchanged ({mastersAfter})");
            return ok && mastersAfter == mastersBefore ? 0 : 1;
        }

        // ------------------------------------------------------------------ testnode

        /// <summary>
        /// His test workflow for a slate pool, as records (his words 2026-10-12): "I'd put the ammo condition onto
        /// the real node then take it off the test one. Then I'd have only the test missions on that node." A
        /// parked test node carries a condition no slate meets (HasKeyword ObjectTypeAmmo on the event's treasure:
        /// every Overtime slate's treasure is LL_LegendaryItem_WeaponList), so it never deals. `on` MOVES that
        /// condition onto the real node (the real pool goes quiet, the test node deals); `off` moves it back.
        /// `--keep a,b` (on only) empties the test node down to the named quests, so the first play is certain.
        /// `--promote` (off only) is the MIGRATION: the test node's quests move into the real node (skipping any
        /// already there) and the test node is left empty. It edits node lists and touches no quest: his ruling
        /// 2026-10-12, "think it's a bit wasteful to rebuilt all the quests just to put them in a different list.
        /// Everytime you build the formid's increment".
        /// Refuses unless exactly one node carries the ammo condition and it is the one being moved FROM, so
        /// the pair can never end with both nodes live or both parked.
        /// </summary>
        private static int TestNode(string[] args, List<Template> templates, bool dry)
        {
            // Positionals are everything after the verb that is not a flag and not --keep's value.
            var pos = new List<string>();
            for (int i = 2; i < args.Length; i++)
            {
                if (args[i].Equals("--keep", StringComparison.OrdinalIgnoreCase)) { i++; continue; }
                if (!args[i].StartsWith("--")) pos.Add(args[i]);
            }
            if (pos.Count != 3 || (pos[0] != "on" && pos[0] != "off"))
            { Console.WriteLine("REFUSED: gen_delve testnode on|off <testNode> <realNode> [--keep quest,quest | --promote] [--dry]"); return 1; }
            bool on = pos[0] == "on";
            string testName = pos[1], realName = pos[2];
            var keepArg = args.SkipWhile(a => !a.Equals("--keep", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
            if (keepArg != null && !on) { Console.WriteLine("REFUSED: --keep is for `on` only."); return 1; }
            bool promote = args.Any(a => a.Equals("--promote", StringComparison.OrdinalIgnoreCase));
            if (promote && on) { Console.WriteLine("REFUSED: --promote is for `off` only (it moves the tested quests into the real pool)."); return 1; }
            var keep = keepArg?.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

            var mods = templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods; testnode knows which plugin only when they agree."); return 1; }
            using var session = PluginSession.Open(mods[0]);
            if (session == null) return 1;
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));

            var test = myMod.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, testName, StringComparison.OrdinalIgnoreCase));
            var real = myMod.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, realName, StringComparison.OrdinalIgnoreCase));
            if (test == null || real == null) { Console.WriteLine($"REFUSED: no Story Manager quest node '{(test == null ? testName : realName)}' in {mods[0]}.esm."); return 1; }
            if (test.FormKey == real.FormKey) { Console.WriteLine("REFUSED: the test node and the real node are the same node."); return 1; }

            const string Ammo = "ObjectTypeAmmo";
            var AmmoKeyword = FormKey.Factory("0F4AE8:Starfield.esm");   // ObjectTypeAmmo, read off the parked node by gen_inspect smtree
            bool IsPark(IConditionGetter c) =>
                c.Data is IHasKeywordConditionDataGetter k && k.FirstParameter.Link.FormKey == AmmoKeyword;
            var from = on ? test : real;
            var to = on ? real : test;
            var parks = from.Conditions.Where(IsPark).ToList();
            if (parks.Count != 1) { Console.WriteLine($"REFUSED: {from.EditorID} carries {parks.Count} {Ammo} condition(s); `{pos[0]}` moves exactly one FROM it."); return 1; }
            if (to.Conditions.Any(IsPark)) { Console.WriteLine($"REFUSED: {to.EditorID} already carries the {Ammo} condition; both nodes would be parked."); return 1; }

            Console.WriteLine();
            Console.WriteLine($"=== TESTNODE {pos[0]}: {test.EditorID} {(on ? "DEALS" : "parked")}, {real.EditorID} {(on ? "parked" : "DEALS")} ===");
            var moved = parks[0].DeepCopy();
            from.Conditions.Remove(parks[0]);
            to.Conditions.Add(moved);
            Console.WriteLine($"  moved {Ammo} condition: {from.EditorID} -> {to.EditorID}");

            if (keep != null)
            {
                var questsByName = myMod.Quests.ToDictionary(q => q.EditorID ?? "", q => q.FormKey, StringComparer.OrdinalIgnoreCase);
                var missing = keep.Where(k => !questsByName.ContainsKey(k)).ToList();
                if (missing.Count > 0) { Console.WriteLine("REFUSED: no quest " + string.Join(", ", missing) + " in the plugin. Nothing written."); return 1; }
                var keepKeys = keep.Select(k => questsByName[k]).ToHashSet();
                var notThere = keep.Where(k => !test.Quests.Any(e => e.Quest.FormKey == questsByName[k])).ToList();
                if (notThere.Count > 0) { Console.WriteLine("REFUSED: " + string.Join(", ", notThere) + " is not on " + test.EditorID + " (deal it there first). Nothing written."); return 1; }
                foreach (var e in test.Quests.Where(e => !keepKeys.Contains(e.Quest.FormKey)).ToList())
                {
                    test.Quests.Remove(e);
                    Console.WriteLine($"  off the test node: {e.Quest.FormKey}");
                }
            }
            var promoted = new List<FormKey>();
            if (promote)
            {
                foreach (var e in test.Quests.ToList())
                {
                    if (!real.Quests.Any(x => x.Quest.FormKey == e.Quest.FormKey))
                    {
                        real.Quests.Add(e.DeepCopy());
                        Console.WriteLine($"  promoted into {real.EditorID}: {myMod.Quests.FirstOrDefault(q => q.FormKey == e.Quest.FormKey)?.EditorID ?? e.Quest.FormKey.ToString()}");
                    }
                    promoted.Add(e.Quest.FormKey);
                    test.Quests.Remove(e);
                }
                if (promoted.Count == 0) { Console.WriteLine($"REFUSED: {test.EditorID} holds no quests to promote. Nothing written."); return 1; }
            }
            Console.WriteLine($"  {test.EditorID} now deals: " + string.Join(", ", test.Quests.Select(e => myMod.Quests.FirstOrDefault(q => q.FormKey == e.Quest.FormKey)?.EditorID ?? e.Quest.FormKey.ToString())));
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath);

            using var reread = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            var t2 = reread.StoryManagerQuestNodes.First(n => n.FormKey == test.FormKey);
            var r2 = reread.StoryManagerQuestNodes.First(n => n.FormKey == real.FormKey);
            int tp = t2.Conditions.Count(IsPark), rp = r2.Conditions.Count(IsPark);
            bool ok = (on ? (tp == 0 && rp == 1) : (tp == 1 && rp == 0));
            if (promote)
            {
                bool allIn = promoted.All(k => r2.Quests.Count(e => e.Quest.FormKey == k) == 1);
                Console.WriteLine($"  [{(allIn ? "OK  " : "FAIL")}] off disk: {promoted.Count} promoted quest(s) each on {r2.EditorID} exactly once, {t2.EditorID} holds {t2.Quests.Count}");
                ok &= allIn && t2.Quests.Count == 0;
            }
            var mastersAfter = string.Join(", ", reread.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            Console.WriteLine($"  [{(ok ? "OK  " : "FAIL")}] off disk: {Ammo} on {t2.EditorID} x{tp}, on {r2.EditorID} x{rp}");
            Console.WriteLine($"  [{(mastersAfter == mastersBefore ? "OK  " : "FAIL")}] the plugin's masters are unchanged ({mastersAfter})");
            return ok && mastersAfter == mastersBefore ? 0 : 1;
        }

        // ------------------------------------------------------------------ unbuild

        /// <summary>
        /// Take a built Delve back OUT of the plugin: every record a build of <paramref name="id"/> made, and its
        /// Story Manager pool entries. His ask 2026-10-12 ("yeah make unbuild"), when the 10-11 lettering renamed
        /// every recipe (duo_delve09 -> duo_delve09a) and the old unlettered builds stayed in the plugin, where the
        /// name-clash lint correctly refused to build the new ones beside them.
        ///
        /// ONE RULE, NOT A COPY OF THE BUILD'S LIST: a record is the build's when its EditorID IS the id or starts
        /// with the id plus "_" (the quest, _item, _msg, _count, _wavepkg, _speaker, _sayN_topic/_scene/_info, the
        /// cloned activators...). The build's own cleanup is ten per-type prefix matches spread over two files; a
        /// second enumeration of those would be the one that drifts. Every type the build has ever minted follows
        /// the naming, so the rule covers types added after this was written.
        ///
        /// Guards: the id must name a QUEST in the plugin (so a stray prefix like "duo" matches nothing and is
        /// refused); only records the plugin itself owns are touched; removerecord's inbound guard (any surviving
        /// FormLink into what leaves refuses the whole write); and the result is re-read off disk: nothing of the
        /// id left, the masters unchanged. --dry lists and writes nothing.
        ///
        /// Records only. Loose files a build deployed (the fragment .pex, voice .wem) are LISTED, not deleted:
        /// they sit in Steam Data and the mod's repo, and a delete there is the owner's call.
        /// </summary>
        private static int Unbuild(string? id, List<Template> templates, bool dry)
        {
            if (string.IsNullOrWhiteSpace(id)) { Console.WriteLine("REFUSED: name the built id, e.g. gen_delve unbuild duo_delve09"); return 1; }
            var mods = templates.Select(t => t.mod).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (mods.Count != 1) { Console.WriteLine("REFUSED: the templates name " + mods.Count + " mods (" + string.Join(", ", mods) + "); unbuild knows which plugin only when they agree."); return 1; }
            string mod = mods[0];

            // The session refuses a plugin that is not in the load order (which this used to let through on
            // File.Exists alone); one it lists but cannot find is refused here, as before.
            using var session = PluginSession.Open(mod);
            if (session == null) return 1;
            if (!File.Exists(session.PluginPath)) { Console.WriteLine("REFUSED: " + session.PluginPath + " does not exist."); return 1; }
            var myMod = session.Mod;
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            var modKey = myMod.ModKey;

            Console.WriteLine();
            Console.WriteLine("=== UNBUILD " + id + " from " + mod + ".esm ===");
            var quest = myMod.Quests.FirstOrDefault(q => string.Equals(q.EditorID, id, StringComparison.OrdinalIgnoreCase));
            if (quest == null) { Console.WriteLine("REFUSED: no quest '" + id + "' in " + mod + ".esm, so this is not a built Delve. Nothing written."); return 1; }

            bool Ours(IMajorRecordGetter r) =>
                r.FormKey.ModKey == modKey && r.EditorID != null &&
                (r.EditorID.Equals(id, StringComparison.OrdinalIgnoreCase) || r.EditorID.StartsWith(id + "_", StringComparison.OrdinalIgnoreCase));

            var matched = myMod.EnumerateMajorRecords().Where(Ours).ToList();
            // A matched record nested inside another matched record (a topic's infos) leaves with its parent.
            var nestedOnly = new HashSet<FormKey>();
            foreach (var m in matched)
                foreach (var c in m.EnumerateMajorRecords())
                    if (c.FormKey != m.FormKey) nestedOnly.Add(c.FormKey);
            var top = matched.Where(m => !nestedOnly.Contains(m.FormKey)).ToList();
            var gone = new HashSet<FormKey>(top.SelectMany(m => m.EnumerateMajorRecords()).Select(r => r.FormKey));
            foreach (var m in top) gone.Add(m.FormKey);

            foreach (var g in matched.GroupBy(m => m.Registration.ClassType.Name).OrderBy(g => g.Key))
                foreach (var m in g.OrderBy(m => m.EditorID))
                    Console.WriteLine($"  {g.Key,-22} {m.EditorID} [{m.FormKey}]{(nestedOnly.Contains(m.FormKey) ? "  (inside its parent)" : "")}");

            int unpooled = 0;
            foreach (var n in myMod.StoryManagerQuestNodes)
                unpooled += n.Quests.Count(e => gone.Contains(e.Quest.FormKey));
            Console.WriteLine($"  {matched.Count} record(s), {gone.Count} leaving with their children; {unpooled} pool entr{(unpooled == 1 ? "y" : "ies")} to drop");

            Console.WriteLine("  loose files a build of " + id + " deployed, NOT touched (the owner's call):");
            Console.WriteLine("    Data/scripts/duo_qf_" + id + ".pex  (the fragment script; its quest is what leaves)");
            Console.WriteLine("    the voice files listed in the mod repo's voicecache/deployed_" + id + ".txt, if it has one");

            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            foreach (var n in myMod.StoryManagerQuestNodes)
                foreach (var e in n.Quests.Where(e => gone.Contains(e.Quest.FormKey)).ToList())
                    n.Quests.Remove(e);
            foreach (var m in top)
                myMod.Remove(m.FormKey, m.Registration.GetterType);

            // removerecord's inbound guard: anything that survives and still links in refuses the whole write.
            var dangling = new List<string>();
            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                if (gone.Contains(rec.FormKey)) continue;
                foreach (var link in rec.EnumerateFormLinks())
                    if (!link.IsNull && gone.Contains(link.FormKey))
                        dangling.Add($"{rec.EditorID ?? "(no EditorID)"} [{rec.FormKey}] -> {link.FormKey}");
            }
            if (dangling.Count > 0)
            {
                Console.WriteLine($"REFUSED: {dangling.Count} surviving link(s) point at what would leave. NOTHING WRITTEN:");
                foreach (var d in dangling.Distinct().Take(20)) Console.WriteLine("  " + d);
                return 1;
            }
            Console.WriteLine($"  inbound guard: 0 surviving links into {gone.Count} removed record(s)");

            session.Close();
            session.Write();
            Console.WriteLine("  wrote " + session.PluginPath + " (" + new FileInfo(session.PluginPath).Length.ToString("N0") + " B)");

            // Off disk: nothing of the id is left, and the masters did not move.
            using var reread = StarfieldMod.CreateFromBinaryOverlay(session.PluginPath, StarfieldRelease.Starfield, session.ReadParams);
            int left = reread.EnumerateMajorRecords().Count(Ours);
            var mastersAfter = string.Join(", ", reread.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));
            bool ok = left == 0 && mastersAfter == mastersBefore;
            Console.WriteLine($"  [{(left == 0 ? "OK  " : "FAIL")}] off disk: {left} record(s) of {id} left");
            Console.WriteLine($"  [{(mastersAfter == mastersBefore ? "OK  " : "FAIL")}] the plugin's masters are unchanged ({mastersAfter})");
            return ok ? 0 : 1;
        }

        private static int WithRecipe(string? path, string dataDir, List<Template> templates,
                                      Func<Recipe, Template, IGameEnvironment<IStarfieldMod, IStarfieldModGetter>, int> body)
        {
            if (path == null) { Console.WriteLine("REFUSED: name a recipe."); return 1; }
            var recipe = LoadRecipe(path, dataDir, templates, out var t);
            if (recipe == null || t == null) return 1;
            using var env = Timing.Timed("env.build", () => GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build());
            return body(recipe, t, env);
        }

        /// <summary>
        /// Read one recipe and refuse it before any game data is touched: not JSON, an unknown key, an id that
        /// is not its file name, or a template that does not exist. Prints its own refusal.
        /// </summary>
        private static Recipe? LoadRecipe(string path, string dataDir, List<Template> templates, out Template? t)
        {
            t = null;
            // A bare id is resolved against the recipes folder, so the common case is short and the
            // explicit path still works for a recipe living anywhere else.
            string file = File.Exists(path) ? path : Path.Combine(dataDir, "recipes", path + ".json");
            if (!File.Exists(file)) { Console.WriteLine("REFUSED: no recipe at " + file); return null; }

            Recipe? recipe;
            string text = File.ReadAllText(file);
            // The schema first: a schema-1 recipe's spoken STRINGS cannot become schema 2's lists, and the parser would call
            // that "not valid JSON", which is true and tells the author nothing.
            try
            {
                using var sd = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                int sv = sd.RootElement.TryGetProperty("schema", out var se) && se.TryGetInt32(out int si) ? si : 0;
                if (sv != 2)
                {
                    Console.WriteLine($"REFUSED: {Path.GetFileName(file)} is schema {sv}; this tool speaks 2 (since 2026-10-19 every spoken field, "
                                      + "broadcast, say, offer.say, recapSay, greeting, reply and each of person.hellos, is a LIST of responses).");
                    return null;
                }
            }
            catch (JsonException ex) { Console.WriteLine("REFUSED: " + Path.GetFileName(file) + " is not valid JSON -- " + ex.Message); return null; }
            try { recipe = JsonSerializer.Deserialize<Recipe>(text, JsonOpts); }
            catch (JsonException ex) { Console.WriteLine("REFUSED: " + Path.GetFileName(file) + " is not valid JSON -- " + ex.Message); return null; }
            if (recipe == null) { Console.WriteLine("REFUSED: " + file + " parsed to nothing."); return null; }
            // ⛔ A KEY THE TOOL DOES NOT KNOW IS A TYPO UNTIL PROVEN OTHERWISE. The reader ignores unknown
            // fields, so "outift" linted clean and built an undressed NPC. A default's harm is the error it
            // suppresses, and the cure is refusing unknown keys (his ask, 2026-10-08: "a linter for the json
            // that validates the outfits/companies etc"). Keys starting with "_" are the authors' notes.
            var unknown = new List<string>();
            using (var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
                UnknownKeys(doc.RootElement, typeof(Recipe), "", unknown);
            if (unknown.Count > 0)
            {
                Console.WriteLine("REFUSED: " + Path.GetFileName(file) + " has key(s) this tool does not know, so they would be IGNORED:");
                foreach (var u in unknown) Console.WriteLine("  " + u);
                Console.WriteLine("  (a key starting with _ is a note and is always allowed)");
                return null;
            }
            // ⛔ THE ID IS THE QUEST'S EDITORID AND A BUILD REPLACES ANY QUEST OF THAT NAME. Two recipes sharing
            // an id would silently overwrite each other's mission, which is the first thing a batch of recipes
            // written quickly will do. So the id must be the file's own name: two files cannot share one.
            string stem = Path.GetFileNameWithoutExtension(file);
            if (!string.Equals(recipe.id, stem, StringComparison.Ordinal))
            { Console.WriteLine($"REFUSED: {Path.GetFileName(file)} has id '{recipe.id}'; a recipe's id must be its file name ('{stem}'), so no two can collide."); return null; }
            if (!System.Text.RegularExpressions.Regex.IsMatch(recipe.id, "^duo_delve[0-9a-z_]+$"))
            { Console.WriteLine($"REFUSED: id '{recipe.id}' must look like duo_delveNN (lower case, digits, underscores): it becomes an EditorID and every clone's prefix."); return null; }
            Console.WriteLine("  recipe: " + file);

            t = templates.FirstOrDefault(x => string.Equals(x.id, recipe.template, StringComparison.OrdinalIgnoreCase));
            if (t == null)
            {
                Console.WriteLine("REFUSED: no template '" + recipe.template + "'. Known: "
                                  + string.Join(", ", templates.Select(x => x.id)));
                return null;
            }
            Console.WriteLine("  template: " + t.id + "  (base " + t.@base + ", driver " + t.driver + ")");
            Console.WriteLine();
            return recipe;
        }

        /// <summary>
        /// EVERY RECIPE, ONE GAME-DATA LOAD, ONE LINE EACH. His ask, 2026-10-08: "I want Jessica to be able to
        /// spam these jsons". A single lint costs a minute of loading the load order; this pays it once. Each
        /// recipe's full lint is captured and only its verdict printed, with the fatal lines under a failure.
        /// Then the checks that only exist ACROSS recipes (names used twice, letter-sibling drift).
        /// </summary>
        private static int LintAll(string dataDir, List<Template> templates)
        {
            var files = Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            Console.WriteLine($"  lint --all: {files.Count} recipe(s) in {Path.Combine(dataDir, "recipes")}");
            using var env = Timing.Timed("env.build", () => GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build());
            var real = Console.Out;
            int failed = 0;
            var names = new List<(string recipe, string field, string name)>();
            int clashes = 0;
            var loaded = new List<Recipe>();
            foreach (var f in files)
            {
                var sw = new StringWriter();
                Console.SetOut(sw);
                bool ok;
                Recipe? r = null;
                try
                {
                    r = LoadRecipe(f, dataDir, templates, out var t);
                    // Never --spread here: each recipe's log is swallowed to one verdict line, so the figures would cost the
                    // placed-ref index and print nowhere.
                    ok = r != null && t != null && Grade(r, t, env, null, false);
                }
                finally { Console.SetOut(real); }
                string log = sw.ToString();
                var lines = log.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
                string warns = lines.FirstOrDefault(l => l.Contains("LINT PASSES"))?.Trim() ?? "";
                Console.WriteLine($"  {(ok ? "PASS" : "FAIL"),-5} {Path.GetFileNameWithoutExtension(f),-24} {(ok ? warns : "")}");
                if (!ok)
                {
                    failed++;
                    foreach (var l in lines.Where(l => l.Contains("[FATAL]") || l.StartsWith("REFUSED") || l.StartsWith("  ") && log.Contains("REFUSED") && l.Contains("did you mean")))
                        Console.WriteLine("        " + l.Trim());
                }
                if (r != null) loaded.Add(r);
                if (r != null)
                {
                    // Named per beat. (Read only at the top level once, which compared nothing on every beats recipe: Jessica, 2026-10-11.)
                    // People only since 2026-10-20: item names left with his ruling that quest items never clash for the player.
                    var named = new List<(string field, string? name)>();
                    for (int i = 0; i < r.beats.Count; i++)
                        named.Add(($"beats[{i + 1}].person.name", r.beats[i].person?.name));
                    foreach (var (field, nm) in named)
                        if (!string.IsNullOrWhiteSpace(nm)) names.Add((r.id, field, nm!));
                }
            }
            Console.WriteLine();
            // (A cross-recipe "two missions share a delivery model" warning stood here from his 2026-10-08 ruling.
            // Retired on his 2026-10-11: "two missions sharing a model isnt really an issue... Im aiming for like
            // 300/700 delves". At that count it fires on everything.)
            // ⭐ LETTER SIBLINGS. His convention, 2026-10-08: "01a and 01b would have the same kinda content but
            // with different lore", as FULL recipes (his pick over a variant file). The cost of full copies is
            // drift: a fix made in 06a and forgotten in 06b. So siblings sharing a number are compared on
            // STRUCTURE (everything that is not lore) and any difference is named. A warning, not a refusal:
            // a sibling that differs on purpose is a new mission number, and that call is the author's.
            foreach (var fam in loaded.GroupBy(x => System.Text.RegularExpressions.Regex.Match(x.id, "^(duo_delve[0-9]+)[a-z]$").Groups[1].Value)
                                      .Where(g => g.Key != "" && g.Count() > 1))
            {
                string Shape(Recipe x, string part) => part switch
                {
                    "template" => x.template,
                    // Everything about a beat that is not lore: where and what kind, how it groups or returns, a hold's
                    // numbers, and WHETHER it carries a person, box, line or item (never their words).
                    "beats" => string.Join(" | ", x.beats.Select(b => string.Join(" ", new[] {
                                    $"{b.at}@{b.place ?? "main"}", b.type, b.group == null ? null : "group:" + b.group,
                                    b.choose == null ? null : "choose:" + b.choose, b.returnTo == null ? null : "return:" + b.returnTo,
                                    b.replace ? "replace" : null, b.waves == null ? null : "waves:" + b.waves,
                                    b.size == null ? null : "size:" + string.Join("-", b.size), b.defend == null ? null : "defend:" + b.defend,
                                    b.stuck == null ? null : "stuck:" + b.stuck, b.style == null ? null : "style:" + string.Join(",", b.style),
                                    b.person == null ? null : "+person", b.message == null ? null : "+box", b.say == null ? null : "+say",
                                    b.item == null ? null : "+item" }.Where(s => s != null)))),
                    "themes" => string.Join(" | ", new[] { x.place.theme, x.place.second?.theme, x.place.third?.theme }
                                    .Select(th => th == null ? "-" : "+" + string.Join(",", th.require.OrderBy(k => k)) + " -" + string.Join(",", th.exclude.OrderBy(k => k)))),
                    // The tiers ride each choose ending.
                    "reward" => string.Concat(x.beats.Select((b, i) => b.reward == null ? "" : $" beat{i + 1}:{b.reward}")),
                    _ => ""
                };
                // Who is at each place is NOT drift: a letter that changes it does so on purpose (his 06b, the medal
                // with an infested find), and the tier moves with the danger (delves README rule 3), so a reward
                // difference between siblings that differ in who is there is expected and not reported.
                string WhoAt(Recipe x) => string.Join(" | ", new[] { x.place, x.place.second, x.place.third }.Select(p => p?.who?.kind ?? "empty"));
                var first = fam.OrderBy(x => x.id).First();
                foreach (var sib in fam.OrderBy(x => x.id).Skip(1))
                    foreach (var part in new[] { "template", "beats", "themes", "reward" })
                        if (Shape(first, part) == Shape(sib, part)) continue;
                        else if (part == "reward" && WhoAt(first) != WhoAt(sib))
                            Console.WriteLine($"  [info ] {sib.id} and {first.id} differ in who is there ('{WhoAt(sib)}' vs '{WhoAt(first)}'), so their rewards may differ: "
                                              + $"'{Shape(sib, part)}' vs '{Shape(first, part)}'.");
                        else
                            Console.WriteLine($"  [warn ] {sib.id} and {first.id} are letter siblings with different {part}: "
                                              + $"'{Shape(sib, part)}' vs '{Shape(first, part)}'. Same number, same content: fix both, or give it its own number.");
            }

            // Two Delves' PEOPLE sharing a name: two different people with one name in a player's world. Across the batch it is
            // a failure. (Item names were in this until 2026-10-20: his "Items marked as quest items can't be removed from the
            // players inventory and they don't need to select it from the inventory", extended here from the per-lint check
            // he retired, by me, said so to him.)
            foreach (var g in names.GroupBy(n => n.name, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(x => x.recipe).Distinct().Count() > 1))
            {
                clashes++;
                Console.WriteLine($"  [FATAL] \"{g.Key}\" is used by {string.Join(", ", g.Select(x => x.recipe + " (" + x.field + ")"))}: two people with one name in a player's world.");
            }
            Console.WriteLine(failed == 0 && clashes == 0 ? $"  ALL {files.Count} RECIPES LINT CLEAN."
                              : $"  {failed} of {files.Count} RECIPE(S) FAIL" + (clashes > 0 ? $", and {clashes} name clash(es) ACROSS recipes." : "."));
            return failed == 0 && clashes == 0 ? 0 : 1;
        }

        /// <summary>
        /// Walk a recipe's JSON against the model it deserialises into and name every key the model does
        /// not have, with its path. Recurses into object properties and list elements. Names match
        /// case-insensitively, as the deserialiser does.
        /// </summary>
        private static void UnknownKeys(JsonElement e, Type t, string path, List<string> outp)
        {
            if (e.ValueKind == JsonValueKind.Array)
            {
                var elem = t.IsGenericType ? t.GetGenericArguments()[0] : null;
                int i = 0;
                if (elem != null) foreach (var item in e.EnumerateArray()) UnknownKeys(item, elem, $"{path}[{i++}]", outp);
                return;
            }
            if (e.ValueKind != JsonValueKind.Object || t == typeof(string)) return;
            // A dictionary's keys are DATA (a template's tokens), never fields: only its values are walked. The first run over
            // templates.json refused every token name until this line existed; recipes carry no dictionary.
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                var vt = t.GetGenericArguments()[1];
                foreach (var kv in e.EnumerateObject()) UnknownKeys(kv.Value, vt, path == "" ? kv.Name : path + "." + kv.Name, outp);
                return;
            }
            var props = t.GetProperties().ToDictionary(p => p.Name, p => p, StringComparer.OrdinalIgnoreCase);
            foreach (var kv in e.EnumerateObject())
            {
                if (kv.Name.StartsWith("_")) continue;
                string here = path == "" ? kv.Name : path + "." + kv.Name;
                if (!props.TryGetValue(kv.Name, out var prop))
                {
                    var near = props.Keys.Where(k => Close(k, kv.Name)).ToList();
                    outp.Add($"{here}" + (near.Count > 0 ? $"   (did you mean {string.Join(" / ", near)}?)" : "") + $"   known here: {string.Join(", ", props.Keys.Where(k => char.IsLower(k[0])))}");
                    continue;
                }
                var pt = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                UnknownKeys(kv.Value, pt, here, outp);
            }
        }

        /// <summary>Two names one edit apart, or one a transposition of the other: the shape of a typo.</summary>
        private static bool Close(string a, string b)
        {
            a = a.ToLowerInvariant(); b = b.ToLowerInvariant();
            if (a == b || Math.Abs(a.Length - b.Length) > 1) return false;
            if (a.Length == b.Length)
            {
                var diff = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToList();
                return diff.Count == 1 || (diff.Count == 2 && diff[1] == diff[0] + 1 && a[diff[0]] == b[diff[1]] && a[diff[1]] == b[diff[0]]);
            }
            var (l, sh) = a.Length > b.Length ? (a, b) : (b, a);
            for (int i = 0; i < l.Length; i++) if (l.Remove(i, 1) == sh) return true;
            return false;
        }

        // ------------------------------------------------------------------ the lint

        /// <summary>
        /// Grade a recipe. Returns true if nothing fatal. Every check prints, pass or fail, because
        /// a lint that only speaks when it is unhappy cannot be told from one that did not run.
        /// </summary>
        private static bool Grade(Recipe r, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                  Dictionary<string, FormKey>? markersOut, bool spread)
        {
            var issues = new List<Issue>();
            void Fatal(string s) => issues.Add(new Issue(true, s));
            void Warn(string s) => issues.Add(new Issue(false, s));

            Console.WriteLine("=== LINT ===");
            using var lap = Timing.Laps("lint");   // FRANKY_TIMING=1; one mark per section below

            lap.Mark("shape+beats");
            // --- shape ---------------------------------------------------------------
            // Schema 2 (2026-10-19, his "Need to add multiple line support"): refused at read, before the parse (LoadRecipe).
            if (r.schema != 2) Fatal($"schema is {r.schema}, this tool speaks 2");
            if (string.IsNullOrWhiteSpace(r.id)) Fatal("no id");
            int maxBeats = t.maxBeats > 0 ? t.maxBeats : t.beats;
            if (r.beats.Count < t.beats)
                Fatal($"recipe has {r.beats.Count} beat(s); template '{t.id}' needs at least {t.beats}. "
                      + "What it cannot do: " + string.Join("; ", t.cannot));
            else if (r.beats.Count > maxBeats)
                Fatal($"recipe has {r.beats.Count} beat(s); template '{t.id}' tops out at {maxBeats}.");

            GradeBeats(r, t, env, Fatal, Warn);
            if (IsBoard(t))
            {
                // The board's own shape, refused rather than half-built: its base has only a place and a marker to give.
                for (int i = 0; i < r.beats.Count; i++)
                    if (t.allows.Count > 0 && !t.allows.Contains(r.beats[i].type ?? ""))
                        Fatal($"beat {i + 1} is a '{r.beats[i].type}' and template '{t.id}' builds only {string.Join(", ", t.allows)}. What it cannot do: {string.Join("; ", t.cannot)}");
                if (r.place.remote != null)
                    Fatal($"place.remote is set; a board mission's place is ALWAYS a request (template '{t.id}'s base carries one), so leave it out.");
                if (string.IsNullOrWhiteSpace(r.prose.card))
                    Fatal("prose.card is required on a board mission: it is the board's description, the card he reads before taking the job.");
                // The board's second place is borrowed bare: nothing on it holds it near the first, so a FOUND second place
                // could be anywhere in the galaxy. A requested one is its own new site (proven 2026-10-14), so that is the shape.
                if (r.beats.Any(b => b.PlaceIndex == 1) && r.place.second?.remote != true)
                    Fatal("a beat is at the second place and place.second.remote is not set; a board mission's second place must be a request (nothing would hold a found one near the first).");
            }
            else if (r.prose.card != null)
                Fatal($"prose.card is set and template '{t.id}' is not a board mission, so it would be silently ignored.");
            if (r.place.civilians != null)
                Fatal("place.civilians retired with the delve4 kind (2026-10-09); on a beats Delve write approach.civilians.");

            lap.Mark("deal");
            // --- deal: which slate pool hands this Delve out (2026-10-11) ------------------------------------
            // Only a node in the template's OWN mod: the build appends to it, and appending to a master's node
            // would write an override of somebody else's record.
            if (r.deal != null)
            {
                var own = ModKey.FromNameAndExtension(t.mod + ".esm");
                var node = OwnOrMaster<IStoryManagerQuestNodeGetter>(new HashSet<ModKey> { own }, env, r.deal);
                if (node == null)
                {
                    var pools = env.LoadOrder.PriorityOrder.WinningOverrides<IStoryManagerQuestNodeGetter>()
                        .Where(n => n.FormKey.ModKey == own).Select(n => n.EditorID).OrderBy(x => x);
                    Fatal($"deal '{r.deal}' is not a Story Manager quest node in {t.mod}. Its nodes: {string.Join(", ", pools)}.");
                }
                else
                {
                    Console.WriteLine($"  deal: {node.EditorID} [{node.FormKey}], {node.Quests.Count} quest(s), {node.Flags} / {node.QuestFlags}");
                    // Structural, not by name: a pool that never dealt this template's base quest is a pool the
                    // base was not built to be started from (another event, other event data). A remote Delve's
                    // place is remotePlaceFrom's, so that is the quest its pool should already deal.
                    // ⛔ The EVENT is routed by the BRANCH, so any node under the same branch counts: since
                    // 2026-10-20 a pool's original quests live in its <pool>_Legacy sibling (gen_delve legacy) and a
                    // rare Delve deals from <pool>_Rare, and a node-only test warned on every Delve of a split pool.
                    string refQ = r.place.remote == true && t.remotePlaceFrom != null ? t.remotePlaceFrom : t.@base;
                    var baseQ = OwnOrMaster<IQuestGetter>(new HashSet<ModKey> { own }, env, refQ);
                    var branchNodes = env.LoadOrder.PriorityOrder.WinningOverrides<IStoryManagerQuestNodeGetter>()
                        .Where(n => n.FormKey.ModKey == own && !node.Parent.IsNull && n.Parent.FormKey == node.Parent.FormKey).ToList();
                    if (!branchNodes.Any(n => n.FormKey == node.FormKey)) branchNodes.Add(node);
                    if (baseQ != null && !branchNodes.Any(n => n.Quests.Any(e => e.Quest.FormKey == baseQ.FormKey)))
                        Warn($"no node under deal '{node.EditorID}''s branch deals {refQ}, the quest this Delve's place is built on; check the pool's event and conditions suit it.");
                }
            }

            lap.Mark("prose+ring+tokens");
            // --- prose, against the style guide (part 33) and the house rule -------------------------------
            foreach (var (where, text) in ProseStrings(r))
            {
                if (where.Contains("message")) continue;   // the message boxes have their own check above
                if (text.Contains('\u2014')) Warn($"{where} carries an em dash; the house rule is none, and vanilla's quest text has zero.");
                else if (text.Any(ch => ch > 126 || (ch < 32 && ch != '\n')))
                    Warn($"{where} carries a non-ASCII or control character; the lane writes plain ASCII.");
            }
            foreach (var (b, i) in r.beats.Select((b, i) => (b, i)))
            {
                var o = b.objective;
                if (string.IsNullOrWhiteSpace(o)) continue;
                int words = o.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (words > 12) Warn($"beat {i + 1} objective is {words} words; vanilla objectives run a median of 4 and never past 12.");
                if (".!?".Contains(o.TrimEnd()[^1])) Warn($"beat {i + 1} objective ends in punctuation; vanilla objectives never do.");
            }

            // --- what may sit on a travel ring --------------------------------------------------------------
            // His rule, 2026-10-14: "Items that are important to someone shouldn't be on travel ring. Infrastructure,
            // Large cargo and hostiles can be on the travel ring". A ring marker is outside and a median 70 m from the
            // centre (card 20261013-113809), so an object there reads as the POI's edge. Only the recipe knows what an
            // object means to someone, so an object beat on a ring must SAY which allowed class it is; fights are the
            // hostiles and are exempt by type. A declaration off the ring is refused too: it would claim nothing.
            string[] ringClasses = { "infrastructure", "large-cargo" };
            foreach (var (b, i) in r.beats.Select((b, i) => (b, i)))
            {
                bool onRing = b.at.StartsWith("RETravel", StringComparison.OrdinalIgnoreCase);
                bool fight = b.type is "hold" or "defeat" or "clear" or "recover";   // recover: the thing is held by a thief
                bool talk = b.type == "talk";
                if (b.ring != null && !ringClasses.Contains(b.ring))
                    Fatal($"beat {i + 1}'s ring is \"{b.ring}\"; it is one of: {string.Join(", ", ringClasses)}.");
                else if (b.ring != null && (!onRing || fight || talk))
                    Fatal($"beat {i + 1} declares ring \"{b.ring}\" but {(onRing ? "is a " + b.type + " beat, which needs none" : "is not on a travel ring (" + b.at + ")")}. Take it out.");
                else if (onRing && !fight && !talk && b.ring == null)
                    Fatal($"beat {i + 1} ({b.type ?? "object"}{(b.name != null ? ", " + b.name : "")}) sits on the travel ring {b.at}, the POI's edge. "
                          + "If it matters to someone, put it on REMarkerMediumLocRef; if it is infrastructure or large cargo, "
                          + "say so with \"ring\": \"infrastructure\" or \"large-cargo\".");
            }

            // --- two beats on top of each other ------------------------------------------------------------
            // Two beats on one marker in one place happen in one spot; two on the same travel RING (A1/A2/A3 or
            // B1/B2/B3) sit 7 to 14 units apart (part 32). A declared return is the one legitimate repeat.
            for (int i = 0; i < r.beats.Count; i++)
                for (int j = i + 1; j < r.beats.Count; j++)
                {
                    var (a, b) = (r.beats[i], r.beats[j]);
                    if (a.PlaceIndex != b.PlaceIndex) continue;
                    bool ret = b.returnTo == i + 1;
                    if (string.Equals(a.at, b.at, StringComparison.OrdinalIgnoreCase))
                    { if (!ret) Warn($"beats {i + 1} and {j + 1} are both on {a.at} at the same place: one spot, two beats."); }
                    else if (a.at.Length > 9 && b.at.Length > 9 && a.at.StartsWith("RETravel") && b.at.StartsWith("RETravel")
                             && a.at[8] == b.at[8])
                        Warn($"beats {i + 1} and {j + 1} are on the same travel ring ({a.at}, {b.at}): they land 7 to 14 m apart.");
                }

            if (string.IsNullOrWhiteSpace(r.prose.name)) Fatal("prose.name is empty");
            if (string.IsNullOrWhiteSpace(r.prose.briefing)) Fatal("prose.briefing is empty");

            // --- prose tokens ---------------------------------------------------------
            // An unknown <Token> is not an error at write time and not an error at load time: it
            // renders to the player as literal angle brackets. Only a lint can catch it.
            foreach (var (where, text) in ProseStrings(r))
            {
                foreach (var tok in Tokens(text))
                    if (!t.tokens.ContainsKey(tok))
                        Fatal($"{where}: <{tok}> is not a token this template supplies. "
                              + "It would print to the player literally. Known: "
                              + string.Join(", ", t.tokens.Keys.Select(k => "<" + k + ">")));
            }

            // --- a second-place token with no second place --------------------------------------------
            // The second place (and its planet) exist only when a beat is there; a token naming them otherwise binds to an
            // alias the build removes or never makes, and prints to the player literally.
            if (!r.beats.Any(b => b.PlaceIndex == 1))
                foreach (var (where, text) in ProseStrings(r))
                    foreach (var tok in Tokens(text).Where(x => x == "SecondPlace" || x == "SecondPlanet").Distinct())
                        Fatal($"{where}: <{tok}> names the second place and no beat is there, so it would print to the player literally.");

            // --- an objective names the place its marker is at -------------------------------------
            // The objective is the line the player follows to the marker, so if it names a place it must name THIS
            // beat's place. His play, 2026-10-13: 03a's crate moved to the second POI and its objective still said
            // "Find the load at the <Place>", pointing him at the wrong site with the marker somewhere else.
            string[] placeTok = { "Place", "SecondPlace", "ThirdPlace" };
            for (int i = 0; i < r.beats.Count; i++)
            {
                var b = r.beats[i];
                if (b.objective == null || b.PlaceIndex < 0) continue;
                var named = Tokens(b.objective).Where(placeTok.Contains).Distinct().ToList();
                if (named.Count > 0 && !named.Contains(placeTok[b.PlaceIndex]))
                    Fatal($"beat {i + 1} objective names <{string.Join(">, <", named)}> but the beat is at the {PlaceKey[b.PlaceIndex]} "
                          + $"(<{placeTok[b.PlaceIndex]}>): the player would be sent to a site its marker is not at.");
            }

            lap.Mark("markers");
            // --- markers --------------------------------------------------------------
            var lcrts = env.LoadOrder.PriorityOrder.WinningOverrides<ILocationReferenceTypeGetter>()
                .Where(x => x.EditorID != null)
                .GroupBy(x => x.EditorID!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().FormKey, StringComparer.OrdinalIgnoreCase);

            var wanted = new List<string>();
            foreach (var b in r.beats) if (!wanted.Contains(b.at, StringComparer.OrdinalIgnoreCase)) wanted.Add(b.at);
            // A support team's landing marker is demanded of its place like a beat's own.
            foreach (var b in r.beats)
                if (b.support?.land is string ld && !wanted.Contains(ld, StringComparer.OrdinalIgnoreCase)) wanted.Add(ld);
            const string MapMarker = "MapMarkerRefType";
            if (!wanted.Contains(MapMarker, StringComparer.OrdinalIgnoreCase)) wanted.Add(MapMarker);

            var keys = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in wanted)
            {
                if (!lcrts.TryGetValue(m, out var k)) { Fatal($"marker '{m}' is not a LocationReferenceType in this load order"); continue; }
                keys[m] = k;
            }
            markersOut?.Clear();
            foreach (var kv in keys) markersOut?.Add(kv.Key, kv.Value);

            lap.Mark("which-place");
            // --- which place each beat is at --------------------------------------------------
            bool anySecond = r.beats.Any(b => b.Second);
            bool anyThird = r.beats.Any(b => b.PlaceIndex == 2);
            for (int i = 0; i < r.beats.Count; i++)
                if (r.beats[i].PlaceIndex < 0)
                    Fatal($"beat {i + 1} names place '{r.beats[i].place}'; the places are \"main\", \"second\" and \"third\"");
            if (anySecond && t.secondPlaceAlias < 0)
                Fatal($"a beat is at the \"second\" place and template '{t.id}' has no second place alias");
            if (anyThird && t.thirdPlace == null)
                Fatal($"a beat is at the \"third\" place and template '{t.id}' creates no third place");
            if (!anyThird && r.place.third != null)
                Warn("place.third is set and no beat is at the third place, so it is never written");
            if (!anySecond && r.place.second != null)
                Warn("place.second is set and no beat is at the second place, so it is never written");
            // A board mission's place is a request already (the base's own), so it is graded as a remote one is.
            bool remote = r.place.remote == true || IsBoard(t);
            // A remote SECOND place (his 2026-10-14, missionboard02a and 10a: a cargo run remote to remote) is a second
            // request beside the main one. No shipped quest does this (556 vanilla requesting quests and Overtime's 140, none
            // with two: manual). PROVEN in his play the same day on duo_delve_twosite01a: "two different POIs on two
            // different Planets". The planets differing is ONE play, and nothing in the request forces it.
            bool secondRemote = r.place.second?.remote == true;
            if (r.place.third?.remote != null)
                Fatal("remote is set on place.third; only the main and the second place can be requested.");
            // THE ORBIT (2026-10-22, 19a): a thing over the main place's planet, used from the ship.
            if (r.place.orbit != null || r.place.second?.orbit != null)
                Fatal("orbit is set on the main or the second place; only place.third can be an orbit.");
            if (r.place.third?.orbit is string ov && !OrbitScopes.ContainsKey(ov))
                Fatal($"place.third.orbit is '{ov}'; it is \"planet\" (over the main place's planet), \"system\" (anywhere in its star system) or \"near\" (MB01's own pick, within place.third.parsecs of the player).");
            if (r.place.third?.orbit == "near" && r.place.third.parsecs == null)
                Fatal("place.third.orbit is \"near\" and place.third.parsecs is not set; it is the limit that keeps the orbit close.");
            if (IsOrbit(r))
            {
                var o = r.place.third!;
                if (t.orbitFrom == null) Fatal($"place.third.orbit is set and template '{t.id}' names no orbitFrom, so there is no orbit to copy.");
                if (o.theme.require.Count > 0 || o.theme.exclude.Count > 0) Fatal("place.third.orbit has a theme; an orbit is not a drawn POI, so a theme would test nothing.");
                if (o.who != null) Fatal("place.third.orbit has a who; nobody stands in orbit.");
                if (o.leash != null || o.reach != null) Fatal("place.third.orbit sets leash or reach; an orbit is over the main place's planet, not a drive from it.");
                for (int i = 0; i < r.beats.Count; i++)
                {
                    var b = r.beats[i];
                    if (b.PlaceIndex != 2) continue;
                    if (!OrbitBeatTypes.Contains(b.type))
                        Fatal($"beat {i + 1} ({b.type}) is in orbit; only {string.Join(", ", OrbitBeatTypes)} are built there, a thing used from the ship (his MB01 satellite).");
                    if (!string.Equals(b.at, OrbitMarker, StringComparison.OrdinalIgnoreCase))
                        Fatal($"beat {i + 1} is in orbit at '{b.at}'; an orbit's markers are its space cell's own, so `at` is {OrbitMarker} (his MB01's, filled from the cell).");
                }
            }
            if (r.place.second?.remote == false)
                Fatal("place.second.remote is false; leave it out for a found second place.");
            // A LOCAL main place with a requested second (10a, his 2026-10-14 "build 10a": here, then another planet) is
            // allowed: the main keeps its leash to the player; the second has no leash and no planet guard, so it lands
            // on whichever planet the planet content manager picks.
            // A third place would be leashed to a second that is on another planet, so it is refused beside a request.
            // An ORBIT third is held to the main place's planet or system, never within a drive of the second, so it may sit
            // beside a second request (his 2026-10-22, 19a: the dungeon a created POI, the orbit kept).
            if (secondRemote && (anyThird || r.place.third != null) && !IsOrbit(r))
                Fatal("place.second.remote is set and the recipe has a third place; a third place is held within a drive of the second, which is on another planet.");
            foreach (var (p, label) in new[] { (r.place, "place"), (r.place.second, "place.second"), (r.place.third, "place.third") })
            {
                if (p == null) continue;
                bool nearOrbit = label == "place.third" && p.orbit == "near";
                if (p.habitability != null && p.remote != true)
                    Fatal($"{label}.habitability shapes a REQUEST, and {label}.remote is not set.");
                if (p.parsecs != null && p.remote != true && !nearOrbit)
                    Fatal($"{label}.parsecs / {label}.habitability shape a REQUEST, and {label}.remote is not set.");
                if (p.parsecs is double ps && !(ps > 0))
                    Fatal($"{label}.parsecs must be above 0 (got {ps}).");
                if (p.habitability == true)
                    Fatal($"{label}.habitability is true; leave it out to keep the request's planet condition, false to drop it.");
            }
            if (!secondRemote && !IsBoard(t))
                foreach (var (where, text) in ProseStrings(r))
                    if (Tokens(text).Contains("SecondPlanet"))
                        Fatal($"{where}: <SecondPlanet> needs place.second.remote; a found second place is on the main place's planet and gets no planet alias of its own.");
            if (secondRemote)
                Console.WriteLine("  second   : a SECOND REQUEST (proven 2026-10-14, two sites on two planets in one play); "
                                  + "no planet guard (two crashed the game; his 2026-10-19 ruling), so it may land on either planet");
            if (r.place.remote == true && t.remotePlaceFrom == null)
                Fatal($"place.remote is set and template '{t.id}' names no remotePlaceFrom, so there is no request to copy.");
            if (remote && (anyThird || r.place.third != null || (!secondRemote && (anySecond || r.place.second != null))))
                Fatal("place.remote is set and the recipe has a second or third place. A remote Delve is ONE requested site "
                      + "(his 2026-10-14 pick); every beat happens there.");
            if (remote && r.place.reach != null)
                Fatal("place.reach is set on a remote Delve; it holds other places near the main one and there are none.");

            lap.Mark("pool");
            // --- the pool, PER PLACE, which is the number the design sits on ---------------------
            // ⭐ Each place alias is drawn on its OWN conditions, so each gets its own pool figure: the
            // markers of the beats that happen there, the map marker, and that place's theme.
            if (!issues.Any(i => i.Fatal))
            {
                var drawPool = DelveCensus.CensusOf(env).Draw;
                var reqPool = remote || secondRemote ? DelveCensus.CensusOf(env).PcmRequestPool() : null;
                if (remote)
                    Console.WriteLine($"  corpus: {reqPool!.Count} place(s) a PCM_QuestGeneral request can put down (gen_delve pcmpool {PoiCensus.PcmGeneralNode}), "
                                      + (IsBoard(t) ? "read as the board base's own request (no PCM type keyword, so tested by no node, as QuestGeneral is), with no parsec limit: the board picks the system. "
                                                    : "on a planet within duo_distanceformission parsecs. ")
                                      + "⚠ Branch conditions are not evaluated: a humanless system reaches fewer");
                else
                    Console.WriteLine($"  corpus: {drawPool.Count} location(s) a place can draw (the POI pool plus every location carrying LocDungeonBossLocRef)");
                var drawn = new Dictionary<int, HashSet<string>>();
                foreach (int pi in PlacesUsed(r))
                {
                    if (pi == 2 && IsOrbit(r))
                    {
                        Console.WriteLine($"  -- the third place is an ORBIT (created at build from {t.orbitFrom}): {(r.place.third!.orbit == "planet" ? "over the main place's planet" : "in the main place's star system")}, no POI pool to draw");
                        continue;
                    }
                    var pool = (pi == 0 && remote) || (pi == 1 && secondRemote) ? reqPool! : drawPool;
                    var here = MarkersAt(r, keys, pi);
                    var theme = ThemeOf(r, pi);
                    Console.WriteLine($"  -- the {PlaceLabel(pi)} place (alias "
                                      + (pi == 2 ? $"CREATED at build, a clone of {t.thirdPlace?.cloneOf}" : PlaceAliasOf(t, pi, -1).ToString()) + ")");
                    drawn[pi] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int all = 0, mapOnly = 0;
                    var perMarker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var m in here.Keys) perMarker[m] = 0;
                    var reqK = ResolveKeywords(env, theme.require, Fatal);
                    var excK = ResolveKeywords(env, theme.exclude, Fatal);
                    foreach (var poi in pool)
                    {
                        foreach (var m in here) if (poi.RefTypes.Contains(m.Value)) perMarker[m.Key]++;
                        if (!here.Values.All(k => poi.RefTypes.Contains(k))) continue;
                        mapOnly++;
                        if (reqK.Any(k => !poi.Keywords.Contains(k))) continue;
                        if (excK.Any(k => poi.Keywords.Contains(k))) continue;
                        all++;
                        drawn[pi].Add(poi.Edid);
                    }
                    foreach (var kv in perMarker)
                        Console.WriteLine($"    {kv.Key,-34} {kv.Value,4} of {pool.Count}  ({100.0 * kv.Value / Math.Max(1, pool.Count):F1}%)");
                    Console.WriteLine($"    all markers together               {mapOnly,4}");
                    Console.WriteLine($"    after the theme predicate          {all,4}   <- the draw pool");
                    string which = $"the {PlaceLabel(pi).ToLowerInvariant()} place";
                    if (all == 0) Fatal($"{which}'s draw pool is ZERO. Nothing would ever select this mission.");
                    else if (all < 40) Warn($"{which}'s draw pool is {all}. Narrow -- the narrowest anything in Overtime leans on is 44.");
                }

                // Two of a Delve's places drawing ONE POI would put two legs of a trail on the same site. Whether the engine
                // de-duplicates location aliases is not established (part 32: eight shipped cargo quests draw pickup and
                // delivery from an identical pool, untested), so the overlap is measured and named rather than trusted.
                // ⛔ TWO ENDINGS MUST BE TWO PLACES, a refusal: the shipped separation mechanism is the THEME PREDICATE,
                // disjoint by construction, so two endings' pools must not share a single POI (the retired choice kind's
                // rule, carried over to choose endings).
                foreach (var (a, b) in new[] { (0, 1), (0, 2), (1, 2) })
                    if (drawn.ContainsKey(a) && drawn.ContainsKey(b) && !(a == 0 && b == 1 && secondRemote))   // a second REQUEST makes a new site on another planet: no shared POI to draw
                    {
                        int both = drawn[a].Intersect(drawn[b], StringComparer.OrdinalIgnoreCase).Count();
                        bool endings = r.beats.Any(x => x.choose != null && x.PlaceIndex == a) && r.beats.Any(x => x.choose != null && x.PlaceIndex == b);
                        if (endings) Console.WriteLine($"  {PlaceLabel(a).ToLowerInvariant()} and {PlaceLabel(b).ToLowerInvariant()} (two endings) overlap on {both} POI(s)");
                        if (both > 0 && endings)
                            Fatal($"two endings' places, {PlaceLabel(a).ToLowerInvariant()} and {PlaceLabel(b).ToLowerInvariant()}, can draw the SAME POI ({both}). "
                                  + "Make their themes disjoint: one requires a keyword the other excludes.");
                        else if (both > 0) Warn($"the {PlaceLabel(a).ToLowerInvariant()} and {PlaceLabel(b).ToLowerInvariant()} places can draw the same POI ({both} in common); give one a theme the other excludes if they must differ.");
                    }

                lap.Mark("drop");
                // --- what the build will DROP from the base --------------------------------------------
                // ⛔ WHY THIS EXISTS: the build rebuilds every keyword condition from the recipe, which is
                // the design, and for a day it silently threw away the base's own "not Natural, not Cave"
                // exclusions, so the Delves drew exactly the places his quest refused. His eye found it
                // ("It's mainly caves and natural locations atm"). The drop stays; it is never silent.
                var baseQ = env.LoadOrder.PriorityOrder.WinningOverrides<IQuestGetter>().FirstOrDefault(q => q.EditorID == t.@base);
                // A remote main place is the remotePlaceFrom quest's alias, so what it would drop is THAT alias's.
                // A board place is the base's own request, so the base is what it drops from.
                bool copied = r.place.remote == true || (secondRemote && t.remotePlaceFrom != null);
                var remoteQ = copied ? env.LoadOrder.PriorityOrder.WinningOverrides<IQuestGetter>().FirstOrDefault(q => q.EditorID == t.remotePlaceFrom) : null;
                if (copied && remoteQ == null) Fatal($"remotePlaceFrom '{t.remotePlaceFrom}' is not a quest in this load order.");
                if (baseQ?.Aliases != null)
                    foreach (int pi in PlacesUsed(r))
                    {
                        if (pi == 2 && IsOrbit(r)) continue;   // an orbit keeps none of the base's place conditions
                        var fromQ =((pi == 0 && r.place.remote == true) || (pi == 1 && secondRemote)) && remoteQ != null ? remoteQ : baseQ;
                        // The third place is a clone, so what it would inherit and drop is its source's.
                        int aid = pi == 2 ? (t.thirdPlace?.cloneOf ?? -1) : pi == 1 && secondRemote ? t.placeAlias : PlaceAliasOf(t, pi, -1);
                        var theme = ThemeOf(r, pi);
                        var la = fromQ.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)aid);
                        foreach (var c in la?.Conditions ?? Enumerable.Empty<IConditionGetter>())
                        {
                            if (c.Data is not ILocationHasKeywordConditionDataGetter kd) continue;
                            var kfk = kd.FirstParameter.Link.FormKey;
                            string kn = env.LinkCache.TryResolve<IKeywordGetter>(kfk, out var kw) ? (kw.EditorID ?? kfk.ToString()) : kfk.ToString();
                            bool excluded = c is IConditionFloatGetter cf && cf.ComparisonValue == 0f;
                            var list = excluded ? theme.exclude : theme.require;
                            if (list.Contains(kn, StringComparer.OrdinalIgnoreCase)) continue;
                            Warn($"the base's alias {aid} {(excluded ? "EXCLUDES" : "REQUIRES")} {kn} and this recipe does not; "
                                 + "the build DROPS it. Put it in the recipe's theme if the mission should keep it.");
                        }
                    }
            }

            lap.Mark("separations");
            // --- how far apart the beats actually land -------------------------------------
            // ⛔ THIS EXISTS BECAUSE COVERAGE IS SILENT ABOUT GEOMETRY. The first playable Delve put
            // its two beats close enough together that the mission read as nothing, and both markers
            // were 99.6% of the pool. Nothing in a coverage table could have predicted it.
            //
            // ⭐ HIS FACT: THE TRAVEL MARKERS ARE THE EDGES OF THE POI. So the two reference rows
            // below are the site's RADIUS and its DIAMETER, and the diameter is the hard ceiling on
            // how far apart any two beats in ONE POI can ever be. No printed verdict and no
            // threshold: three numbers and the author decides, because "far enough" is a question
            // about the fiction and not about the records.
            //
            // ⚠ OPT-IN SINCE 2026-10-20 (--spread), his "try 1 and 2": it never fails a lint, and its first placed-object
            // lookup makes Mutagen index every placed ref in the load order, 26-42 s of a ~60-80 s lint (FRANKY_TIMING=1).
            // Skipped out loud, never silently, so an author still sees where it went.
            if (!spread && !issues.Any(i => i.Fatal) && r.beats.Count >= 2)
                Console.WriteLine("\n  separation: skipped (it only prints; add --spread, or run gen_delve spread <markerA> <markerB>)");
            if (spread && !issues.Any(i => i.Fatal) && r.beats.Count >= 2)
            {
                Console.WriteLine();
                Console.WriteLine("  separation, METRES (1 unit = 1 m, read off his HUD 2026-09-24)");
                var want = new List<(string, string, string)>
                {
                    ("THIS RECIPE", r.beats[0].at, r.beats[1].at),
                    ("site radius", "RECenterLocRef", "RETravelA1LocRef"),
                    ("site diameter (the ceiling)", "RETravelA1LocRef", "RETravelB1LocRef"),
                };
                if (r.beats[0].PlaceIndex != r.beats[1].PlaceIndex)
                {
                    // Two POIs: beats 1 and 2 are not in one site, so a marker-to-marker distance between
                    // them is not a thing. Their separation is the second place's leash (see the warn).
                    Console.WriteLine("    THIS RECIPE: beats 1 and 2 are at DIFFERENT POIs; no in-site separation applies.");
                    want.RemoveAt(0);
                }
                foreach (var (label, a, b) in want)
                {
                    var v = DelveCensus.CensusOf(env).Separations(a, b);
                    if (v.Count == 0) { Console.WriteLine($"    {label,-30} {a} <-> {b}: no POI carries both"); continue; }
                    v.Sort();
                    Console.WriteLine($"    {label,-30} n={v.Count,4}  min {v[0],6:F0}  p25 {PoiCensus.Pct(v, .25),6:F0}"
                                      + $"  med {PoiCensus.Pct(v, .5),6:F0}  p75 {PoiCensus.Pct(v, .75),6:F0}  max {v[^1],6:F0}   ({a} <-> {b})");
                }
                Console.WriteLine("    ⚠ Two markers from the SAME travel ring (A1/A2/A3, or B1/B2/B3) sit on top of each");
                Console.WriteLine("      other: median 7 to 14 units. An A marker paired with a B marker is the widest");
                Console.WriteLine("      a single POI offers, and it is about double what the centre gives.");
            }

            lap.Mark("leash+reach");
            // --- leash, the stated v1 limit ---------------------------------------------
            if (!string.IsNullOrWhiteSpace(r.place.leash))
            {
                var g = env.LoadOrder.PriorityOrder.WinningOverrides<IGlobalGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, r.place.leash, StringComparison.OrdinalIgnoreCase));
                if (g == null) Fatal($"leash '{r.place.leash}' is not a Global in this load order");
                else Warn($"leash '{r.place.leash}' is ADVISORY in v1: the base's own distance condition is preserved "
                          + "verbatim and this value is not written. Constructing a GetDistance condition is unproven.");
            }

            // --- reach: how close the other places are held (written, unlike leash) -------------------
            if (r.place.reach != null)
            {
                var g = OwnOrMaster<IGlobalGetter>(Allowed(env, t), env, r.place.reach);
                if (g == null) Fatal($"place.reach '{r.place.reach}' is not a Global in {t.mod} or one of its masters.");
                else if (r.place.second == null && r.place.third == null)
                    Fatal($"place.reach is set but the recipe has no second or third place, so there is nothing to hold near the main one.");
                else Console.WriteLine($"  place.reach: every other place within {g.EditorID} of the main place and of each other");
            }

            lap.Mark("report");
            // --- report ------------------------------------------------------------------
            Console.WriteLine();
            foreach (var i in issues) Console.WriteLine((i.Fatal ? "  [FATAL] " : "  [warn ] ") + i.Text);
            int fatal = issues.Count(i => i.Fatal);
            Console.WriteLine();
            Console.WriteLine(fatal == 0
                ? $"  LINT PASSES ({issues.Count - fatal} warning(s))."
                : $"  LINT FAILS: {fatal} fatal, {issues.Count - fatal} warning(s).");
            return fatal == 0;
        }

        /// <summary>The places a recipe uses, by index: main always, then any a beat names.</summary>
        private static List<int> PlacesUsed(Recipe r)
            => new[] { 0 }.Concat(r.beats.Select(b => b.PlaceIndex).Where(i => i > 0)).Distinct().OrderBy(i => i).ToList();

        private static Theme ThemeOf(Recipe r, int place)
            => place == 0 ? r.place.theme : (place == 1 ? r.place.second : r.place.third)?.theme ?? new Theme();

        private static string PlaceLabel(int place) => place == 0 ? "MAIN" : place == 1 ? "SECOND" : "THIRD";

        /// <summary>
        /// The location alias a place is drawn on. The third place does not exist on the base: it is
        /// created at build time, so its id is handed in (-1 at lint time, where nothing is built yet).
        /// </summary>
        private static int PlaceAliasOf(Template t, int place, int thirdId)
            => place == 0 ? t.placeAlias : place == 1 ? t.secondPlaceAlias : thirdId;

        private static Dictionary<string, FormKey> MarkersAt(Recipe r, Dictionary<string, FormKey> all, int place)
        {
            // The markers one place must carry: its own beats' markers, plus the map marker every
            // shipped target place carries (the player has to be able to navigate there).
            var outp = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            foreach (var b in r.beats.Where(b => b.PlaceIndex == place))
            {
                if (all.TryGetValue(b.at, out var k)) outp[b.at] = k;
                if (b.support?.land is string ld && all.TryGetValue(ld, out var lk)) outp[ld] = lk;
            }
            if (all.TryGetValue("MapMarkerRefType", out var mm)) outp["MapMarkerRefType"] = mm;
            return outp;
        }

        // (An item-name check stood here from 2026-09-24: a pickup's name matching ANY named record in the load order was
        // fatal. Retired 2026-10-20 on his ruling: "we don't care about this. Items marked as quest items can't be removed
        // from the players inventory and they don't need to select it from the inventory." It read every record in the
        // load order, ~36 s of every lint and build of a recipe with an item.)

        private static IEnumerable<(string, string)> ProseStrings(Recipe r)
        {
            yield return ("prose.name", r.prose.name ?? "");
            yield return ("prose.briefing", r.prose.briefing ?? "");
            if (r.prose.card != null) yield return ("prose.card", r.prose.card);   // Jessica's catch on missionboard02a: the card was never token-checked
            for (int i = 0; i < r.beats.Count; i++)
            {
                yield return ($"beat {i + 1} objective", r.beats[i].objective ?? "");
                if (r.beats[i].journal != null) yield return ($"beat {i + 1} journal", r.beats[i].journal!);
                if (r.beats[i].message?.title != null) yield return ($"beat {i + 1} message title", r.beats[i].message!.title!);
                if (r.beats[i].message?.text != null) yield return ($"beat {i + 1} message text", r.beats[i].message!.text!);
            }
            if (r.offer?.journal != null) yield return ("offer journal", r.offer.journal);
            if (r.offer?.message?.title != null) yield return ("offer message title", r.offer.message.title);
            if (r.offer?.message?.text != null) yield return ("offer message text", r.offer.message.text);
            if (r.offer?.say != null) foreach (var s in r.offer.say) yield return ("offer say", s);
            if (r.recapSay != null) foreach (var s in r.recapSay) yield return ("recapSay", s);
        }

        private static IEnumerable<string> Tokens(string s)
        {
            int i = 0;
            while ((i = s.IndexOf('<', i)) >= 0)
            {
                int j = s.IndexOf('>', i);
                if (j < 0) yield break;
                string inner = s[(i + 1)..j];
                // <Alias=X> is the engine's own spelling and is deliberately NOT graded here: a
                // recipe should not be writing one, and if it does it is naming a real alias.
                if (!inner.Contains('=')) yield return inner;
                i = j + 1;
            }
        }

        private static List<FormKey> ResolveKeywords(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env,
                                                    List<string> names, Action<string> fatal)
        {
            var outp = new List<FormKey>();
            foreach (var n in names)
            {
                var k = env.LoadOrder.PriorityOrder.WinningOverrides<IKeywordGetter>()
                    .FirstOrDefault(x => string.Equals(x.EditorID, n, StringComparison.OrdinalIgnoreCase));
                if (k == null) fatal($"theme keyword '{n}' does not resolve");
                else outp.Add(k.FormKey);
            }
            return outp;
        }


        /// <summary>
        /// EVERY MODEL PATH SOME RECORD USES, for `gen_delve models` only. A vanilla mesh lives in the game's archives,
        /// not in Data or the mod's own, so FindAsset cannot see it; "some record uses this path" is the test that can.
        ///
        /// ⚠ THE COST, measured 2026-10-20 (FRANKY_TIMING=1 plus a per-plugin probe): ~36 s on Defiant, spread over
        /// every plugin. The 16 base masters are ~24 s of it (Starfield.esm 11 s alone), enabled mods ~4.6 s, disabled
        /// ones ~5.6 s. Walking only the modeled types was tried and dropped the same day: no faster, and it MISSED
        /// models (WinningOverrides(Type) on the object-mod siblings returns the whole shared group). So it runs once,
        /// as a final test over every recipe, and never inside a lint (his ruling, 2026-10-20: "this can be a single
        /// final test at the end").
        /// </summary>
        private static HashSet<string> UsedModels(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rec in env.LoadOrder.PriorityOrder.WinningOverrides<IMajorRecordGetter>())
                if (rec is IModeledGetter mg && mg.Model?.File != null) used.Add(mg.Model.File.DataRelativePath.Path);
            return used;
        }

        /// <summary>
        /// `gen_delve models [recipe ...]`: THE FINAL TEST that every beat model resolves, over every recipe (or the ones
        /// named) in one process, so the whole-load-order walk is paid once. Until 2026-10-20 this ran inside every lint
        /// and build, and a vanilla mesh made each one pay ~36 s (his: "this can be a single final test at the end").
        /// A model resolves when it is in Data, in the template mod's archives, or used by some record. A recipe the
        /// reader refuses is SKIPPED OUT LOUD and fails the run: its models were not checked, and a final test that
        /// passes over an unchecked recipe has said something it does not know.
        /// </summary>
        private static int ModelCheck(string dataDir, List<Template> templates, List<string> named)
        {
            var files = named.Count > 0 ? named
                : Directory.GetFiles(Path.Combine(dataDir, "recipes"), "*.json").OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            var want = new List<(string recipe, int beat, string model, string mod)>();
            int skipped = 0;
            var real = Console.Out;
            foreach (var f in files)
            {
                Recipe? r; Template? t;
                var sw = new StringWriter();
                Console.SetOut(sw);
                try { r = LoadRecipe(f, dataDir, templates, out t); }
                finally { Console.SetOut(real); }
                if (r == null || t == null)
                {
                    skipped++;
                    Console.WriteLine($"  SKIP  {Path.GetFileNameWithoutExtension(f)}: the recipe is refused, so its models are NOT checked (gen_delve lint says why)");
                    continue;
                }
                for (int i = 0; i < r.beats.Count; i++)
                    if (!string.IsNullOrWhiteSpace(r.beats[i].model)) want.Add((r.id, i + 1, r.beats[i].model!, t.mod));
            }
            using var env = Timing.Timed("env.build", () => GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build());
            HashSet<string>? used = null;
            int missing = 0;
            foreach (var (id, beat, model, mod) in want)
            {
                if (FindAsset(env.DataFolderPath.Path, mod, model) != "") continue;
                used ??= Timing.Timed("models.used", () => UsedModels(env));
                if (used.Contains(model)) continue;
                missing++;
                Console.WriteLine($"  [FATAL] {id} beat {beat}: model '{model}' is used by no record and is not in Data or {mod}'s archives; it would render nothing.");
            }
            int checkedRecipes = files.Count - skipped;
            Console.WriteLine(missing == 0
                ? $"  ALL {want.Count} BEAT MODEL(S) RESOLVE, over {checkedRecipes} recipe(s)" + (skipped > 0 ? $"; {skipped} recipe(s) SKIPPED, unchecked." : ".")
                : $"  {missing} of {want.Count} BEAT MODEL(S) WOULD RENDER NOTHING" + (skipped > 0 ? $"; {skipped} recipe(s) SKIPPED, unchecked." : "."));
            return missing == 0 && skipped == 0 ? 0 : 1;
        }

        // ------------------------------------------------------------------ the build

        /// <summary>
        /// Where a data-relative asset path actually lives: the name of the first "{mod} - *.ba2" whose
        /// BTDX name table lists it, else "loose" if the file is in Data, else "" (nowhere). Archives
        /// are checked FIRST because loose-only is the state that ships broken.
        /// </summary>
        private static string FindAsset(string dataDir, string mod, string path)
        {
            string want = path.Replace('\\', '/').ToLowerInvariant();
            foreach (var ba2 in Directory.GetFiles(dataDir, mod + " - *.ba2"))
            {
                using var f = File.OpenRead(ba2);
                using var br = new BinaryReader(f);
                if (new string(br.ReadChars(4)) != "BTDX") continue;
                f.Seek(12, SeekOrigin.Begin);
                uint count = br.ReadUInt32();
                long nameTable = (long)br.ReadUInt64();
                f.Seek(nameTable, SeekOrigin.Begin);
                for (uint i = 0; i < count; i++)
                {
                    int len = br.ReadUInt16();
                    string name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(len));
                    if (name.Replace('\\', '/').ToLowerInvariant() == want) return Path.GetFileName(ba2);
                }
            }
            return File.Exists(Path.Combine(dataDir, path)) ? "loose" : "";
        }

        /// <summary>FromBase: start from the base quest's clone even when the template has a shell (the A/B proof's A side;
        /// its exit is every template having a shell that has passed the A/B). EmitDir: with --dry, write the would-be
        /// plugin and the emitted model there instead of nowhere.</summary>
        private sealed record BuildOptions(bool FromBase, string? EmitDir, string DataDir, bool Spread);

        /// <summary>
        /// THE RECIPE EMITS THE MODEL. Once every rule has run, the built quest is read into gen_questmodel's model (a
        /// field the model cannot hold refuses the build, by name), the model is written to data/delves/models/&lt;id&gt;.json
        /// beside its recipe, and the quest that goes into the plugin is WRITTEN FROM THAT FILE'S TEXT, every object new.
        /// So the model is the build's output and the record is a function of it, the same way the fragment script's
        /// source is committed under papyrus/gen. A dry run writes no model unless --emit names a folder.
        /// </summary>
        /// <summary>
        /// gen_delve shell &lt;template&gt;: the ONE time the base quest is read for a template. Its model, cut to the fields a
        /// build ever took off it (gen_questmodel.ShellFields), goes to data/delves/shells/&lt;template&gt;.json; from then on
        /// the file is ours and the build reads it instead. A refusal inside those fields refuses the shell; one outside
        /// them (a field the build never took) is printed and left behind, because the build never wrote it either.
        /// </summary>
        private static int MakeShell(string? id, string dataDir, List<Template> templates)
        {
            var t = templates.FirstOrDefault(x => x.id == id);
            if (t == null) { Console.WriteLine($"REFUSED: no template '{id}'. gen_delve templates lists them."); return 1; }
            using var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build();
            string modFile = Path.Combine(env.DataFolderPath, t.mod + ".esm");
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);
            using var mod = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var b = mod.Quests.FirstOrDefault(q => q.EditorID == t.@base);
            if (b == null) { Console.WriteLine($"REFUSED: no base quest '{t.@base}' in {modFile}"); return 1; }
            var full = gen_questmodel.ReadModel(b, out var refusals);
            var inside = refusals.Where(gen_questmodel.InShell).ToList();
            foreach (var x in refusals.Except(inside)) Console.WriteLine("  left out: " + x + "  (outside the shell's fields; the build never took it)");
            if (inside.Count > 0)
            {
                foreach (var x in inside) Console.WriteLine("REFUSED: " + x);
                return 1;
            }

            // THE PARTS: every alias the build takes off ANOTHER quest, read here once, so the build reads none. All of them
            // are read before either file is written: a refusal in one writes neither.
            var parts = new Parts();
            var partRefusals = new List<string>();
            QAlias Part(IAQuestAliasGetter a, string path)
            {
                var m = gen_questmodel.ReadAlias(a, path, out var rf);
                partRefusals.AddRange(rf);
                return m;
            }
            if (t.remotePlaceFrom != null)
            {
                var rq = mod.Quests.FirstOrDefault(q => q.EditorID == t.remotePlaceFrom);
                var ra = rq?.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(a => a.ID == (uint)t.placeAlias);
                if (ra?.ALPS == null) { Console.WriteLine($"REFUSED: remotePlaceFrom '{t.remotePlaceFrom}' has no location alias {t.placeAlias} carrying a PCM request (ALPS) in {modFile}."); return 1; }
                parts.remotePlaceFrom = $"{rq!.EditorID} [{rq.FormKey}]";
                parts.remotePlace = Part(ra, $"{rq.EditorID}.Aliases[{t.placeAlias}]");
            }
            if (t.borrowFrom != null)
            {
                var bq = mod.Quests.FirstOrDefault(q => q.EditorID == t.borrowFrom);
                if (bq == null) { Console.WriteLine($"REFUSED: borrowFrom '{t.borrowFrom}' is not a quest in {modFile}."); return 1; }
                parts.borrowFrom = $"{bq.EditorID} [{bq.FormKey}]";
                foreach (var ba in t.borrowAliases)
                {
                    // The build looks for a ref alias first, then a location alias, by the source id: the same here.
                    IAQuestAliasGetter? a = bq.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(x => x.ID == (uint)ba.from);
                    a ??= bq.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(x => x.ID == (uint)ba.from);
                    if (a == null) { Console.WriteLine($"REFUSED: {bq.EditorID} has no ref or location alias {ba.from} to borrow."); return 1; }
                    parts.borrowed.Add(Part(a, $"{bq.EditorID}.Aliases[{ba.from}]"));
                }
            }
            if (TakesWaves(t))
            {
                var sfm = env.LoadOrder[0].Mod;
                var wq = sfm?.ModKey.FileName == "Starfield.esm" ? sfm.Quests.FirstOrDefault(q => q.FormKey.ID == WaveQuestId) : null;
                var w = WaveCollectionOf(wq);
                if (w == null) { Console.WriteLine($"REFUSED: the wave collection ({WaveMemberName} on [{WaveQuestId:X6}:Starfield.esm]) is not in Starfield.esm."); return 1; }
                parts.wavesFrom = $"{wq!.EditorID} [{wq.FormKey}]";
                parts.waves = Part(w, $"{wq.EditorID}.{WaveMemberName}");
            }
            if (t.orbitFrom != null)
            {
                var oq = mod.Quests.FirstOrDefault(q => q.EditorID == t.orbitFrom);
                if (oq == null || t.orbitAliases == null) { Console.WriteLine($"REFUSED: orbitFrom '{t.orbitFrom}' is not a quest in {modFile}, or the row names no orbitAliases."); return 1; }
                parts.orbitFrom = $"{oq.EditorID} [{oq.FormKey}]";
                foreach (int oid in t.orbitAliases.All)
                {
                    // A collection alias's id is its member's, so it is found through its one member.
                    IAQuestAliasGetter? a = oq.Aliases.OfType<IQuestLocationAliasGetter>().FirstOrDefault(x => x.ID == (uint)oid);
                    a ??= oq.Aliases.OfType<IQuestReferenceAliasGetter>().FirstOrDefault(x => x.ID == (uint)oid);
                    a ??= oq.Aliases.OfType<IQuestCollectionAliasGetter>().FirstOrDefault(c => c.Collection.Count == 1 && c.Collection[0].ReferenceAlias?.ID == (uint)oid);
                    if (a == null) { Console.WriteLine($"REFUSED: {oq.EditorID} has no alias {oid} for the orbit."); return 1; }
                    parts.orbit.Add(Part(a, $"{oq.EditorID}.Aliases[{oid}]"));
                }
            }
            if (partRefusals.Count > 0)
            {
                foreach (var x in partRefusals) Console.WriteLine("REFUSED (a part): " + x);
                return 1;
            }

            string dir = Path.Combine(dataDir, "shells");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, t.id + ".json");
            File.WriteAllText(file, gen_questmodel.ToJson(gen_questmodel.ShellOf(full)));
            Console.WriteLine($"  shell    : {file} from {t.@base} [{b.FormKey}]; set the template row's \"shell\": \"shells/{t.id}.json\"");
            if (parts.remotePlace == null && parts.borrowed.Count == 0 && parts.waves == null && parts.orbit.Count == 0)
            {
                Console.WriteLine("  parts    : none; this template takes nothing off another quest");
                return 0;
            }
            string pfile = Path.Combine(dir, t.id + ".parts.json");
            File.WriteAllText(pfile, JsonSerializer.Serialize(parts, gen_questmodel.JsonOptions));
            Console.WriteLine($"  parts    : {pfile}: " + string.Join(", ", new[] {
                                  parts.remotePlace == null ? null : $"the remote request from {parts.remotePlaceFrom}",
                                  parts.borrowed.Count == 0 ? null : $"{parts.borrowed.Count} borrowed alias(es) from {parts.borrowFrom}",
                                  parts.waves == null ? null : $"the wave collection from {parts.wavesFrom}",
                                  parts.orbit.Count == 0 ? null : $"{parts.orbit.Count} orbit alias(es) from {parts.orbitFrom}" }.Where(s => s != null))
                              + $"; set the template row's \"parts\": \"shells/{t.id}.parts.json\"");
            return 0;
        }

        private static int EmitModel(StarfieldMod myMod, ref Quest quest, Recipe r, BuildOptions opts, bool dry)
        {
            var model = gen_questmodel.ReadModel(quest, out var refusals);
            if (refusals.Count > 0)
            {
                foreach (var x in refusals) Console.WriteLine("REFUSED: the built quest has a field the model cannot hold: " + x);
                return refusals.Count;
            }
            string json = gen_questmodel.ToJson(model);
            if (!dry || opts.EmitDir != null)
            {
                string dir = opts.EmitDir ?? Path.Combine(opts.DataDir, "models");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, r.id + ".json"), json);
            }
            var built = gen_questmodel.WriteModel(gen_questmodel.FromJson(json));
            if (opts.EmitDir != null)
            {
                // The in-run proof: the quest the rules built and the one written from the model's text must be the same bytes.
                bool same = gen_questmodel.SameBytes(quest, built, out int la, out int lb);
                Console.WriteLine(same ? $"  emit     : the quest written from the model is byte-identical to the one the rules built ({la:N0} B)"
                                       : $"REFUSED: the quest written from the model differs from the one the rules built ({la:N0} B vs {lb:N0} B)");
                if (!same) return 1;
            }
            myMod.Quests.Remove(quest.FormKey);
            myMod.Quests.Add(built);
            quest = built;
            Console.WriteLine($"  model    : emitted, {model.Stages.Count} stages, {model.Aliases.Count} aliases, {model.Children?.Scenes.Count ?? 0} scenes, " +
                              $"{model.Children?.Topics.Count ?? 0} topics; the quest written is built from it");
            return 0;
        }

        private static int Build(Recipe r, Template t, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, bool dry, BuildOptions opts)
        {
            var markers = new Dictionary<string, FormKey>(StringComparer.OrdinalIgnoreCase);
            if (!Grade(r, t, env, markers, opts.Spread))
            { Console.WriteLine("\n=== lint failed. NOTHING WRITTEN. ==="); return 1; }

            Console.WriteLine();
            Console.WriteLine("=== BUILD ===");
            using var lap = Timing.Laps("build");   // FRANKY_TIMING=1; the lint above keeps its own laps

            lap.Mark("read-plugin");
            string modFile = Path.Combine(env.DataFolderPath, t.mod + ".esm");
            if (!File.Exists(modFile)) { Console.WriteLine("REFUSED: " + modFile + " does not exist."); return 1; }
            var readParams = gen_quest_main.BuildReadParams(env.LoadOrder);


            var myMod = StarfieldMod.CreateFromBinary(modFile, StarfieldRelease.Starfield, readParams);
            gen_quest_main.FixNextFormId(myMod);
            var mastersBefore = string.Join(", ", myMod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String));

            lap.Mark("quest+places");
            // ⭐ TRUE CONSTRUCTION (his ruling, 2026-10-15): with a shell, the quest is made with `new` from the template's
            // shell model and the base quest is never read here. Without one (or with --from-base, the A/B proof), the old
            // clone. Either way `sourceKey` is the base's FormKey, which the starting quest's self-links still point at.
            QModel? shell = null;
            if (t.shell != null && !opts.FromBase)
            {
                string shellPath = Path.Combine(opts.DataDir, t.shell);
                if (!File.Exists(shellPath)) { Console.WriteLine($"REFUSED: the template's shell {shellPath} does not exist; `gen_delve shell {t.id}` writes it."); return 1; }
                shell = gen_questmodel.FromJson(File.ReadAllText(shellPath));
            }
            // ⭐ AND ITS PARTS (his "close the 2 places", 2026-10-15): with a shell, NO quest is read in this build, so the
            // aliases it copies off other quests come from the parts file. A shell without the parts its template takes is
            // refused by name, never quietly filled from the quest.
            Parts? parts = null;
            if (shell != null && TakesParts(t))
            {
                if (t.parts == null) { Console.WriteLine($"REFUSED: template '{t.id}' has a shell and no parts, so this build would read another quest; `gen_delve shell {t.id}` writes both."); return 1; }
                string partsPath = Path.Combine(opts.DataDir, t.parts);
                if (!File.Exists(partsPath)) { Console.WriteLine($"REFUSED: the template's parts {partsPath} do not exist; `gen_delve shell {t.id}` writes them."); return 1; }
                parts = JsonSerializer.Deserialize<Parts>(File.ReadAllText(partsPath), gen_questmodel.JsonOptions);
                // A parts file read off a quest the row no longer names is the wrong file, whatever it holds.
                string? Stale(string? named, string? readFrom) => named == null ? null
                    : readFrom == null || !readFrom.StartsWith(named + " [", StringComparison.Ordinal) ? $"{named} (the file was read from {readFrom ?? "nothing"})" : null;
                var mismatch = parts == null ? new List<string> { "the file is empty" }
                    : new[] { Stale(t.remotePlaceFrom, parts.remotePlaceFrom), Stale(t.borrowFrom, parts.borrowFrom), Stale(t.orbitFrom, parts.orbitFrom) }.Where(s => s != null).Select(s => s!).ToList();
                if (mismatch.Count > 0) { Console.WriteLine($"REFUSED: {t.parts} does not match template '{t.id}': {string.Join("; ", mismatch)}. `gen_delve shell {t.id}` rewrites it."); return 1; }
                Console.WriteLine($"  parts    : {t.parts}, read in place of the quests they came from");
            }
            var source = shell == null ? myMod.Quests.FirstOrDefault(q => q.EditorID == t.@base) : null;
            if (shell == null && source == null) { Console.WriteLine("REFUSED: no base quest '" + t.@base + "' in " + t.mod); return 1; }
            FormKey sourceKey = shell != null ? FormKey.Factory(shell.FormKey) : source!.FormKey;

            var stale = myMod.Quests.Where(q => q.EditorID == r.id).Select(q => q.FormKey).ToList();
            foreach (var k in stale) myMod.Quests.Remove(k);
            if (stale.Count > 0) Console.WriteLine("  cleaned  : " + stale.Count + " previous " + r.id);

            Quest clone;
            if (shell != null)
            {
                clone = new Quest(myMod) { EditorID = r.id };
                gen_questmodel.ApplyShell(clone, shell);
                Console.WriteLine($"  shell    : {t.shell} (from {shell.EditorID}), built with new; the base quest is not read");
            }
            else
            {
                var copy = source!.DeepCopy();
                clone = new Quest(myMod)
                {
                    EditorID = r.id,
                    Name = copy.Name,
                    Aliases = copy.Aliases,
                    Components = copy.Components,
                    Data = copy.Data,
                    Keywords = copy.Keywords,
                    Location = copy.Location,
                    MajorFlags = copy.MajorFlags,
                    Objectives = copy.Objectives,
                    MissionTypeKeyword = copy.MissionTypeKeyword,
                    QuestType = copy.QuestType,
                    Stages = copy.Stages,
                    Summary = copy.Summary,
                    VirtualMachineAdapter = copy.VirtualMachineAdapter,
                    MissionBoardDescription = copy.MissionBoardDescription,
                    MissionBoardInfoPanels = copy.MissionBoardInfoPanels,
                };
                Console.WriteLine($"  clone    : of the base quest {t.@base}" + (t.shell != null ? " (--from-base)" : ""));
            }
            myMod.Quests.Add(clone);

            // POOLS. The previous build's quest was just removed, so any node entry pointing at it would dangle:
            // remove those from EVERY node (which also handles a recipe whose deal changed or was dropped), then
            // append the new quest to the node the recipe names, once.
            int unpooled = 0;
            foreach (var n in myMod.StoryManagerQuestNodes)
            {
                var drop = n.Quests.Where(e => stale.Contains(e.Quest.FormKey)).ToList();
                foreach (var e in drop) n.Quests.Remove(e);
                unpooled += drop.Count;
            }
            if (unpooled > 0) Console.WriteLine($"  unpooled : {unpooled} entr{(unpooled == 1 ? "y" : "ies")} for the previous {r.id}");
            if (r.deal != null)
            {
                var pool = myMod.StoryManagerQuestNodes.FirstOrDefault(n => string.Equals(n.EditorID, r.deal, StringComparison.OrdinalIgnoreCase));
                if (pool == null) { Console.WriteLine($"REFUSED: no Story Manager quest node '{r.deal}' in {t.mod}."); return 1; }
                pool.Quests.Add(new StoryManagerQuest { Quest = clone.ToNullableLink<IQuestGetter>() });
                Console.WriteLine($"  dealt    : {r.id} into {pool.EditorID}, now {pool.Quests.Count} quests");
            }

            // A clone that does not repoint its self-links drives the SOURCE quest's aliases,
            // silently, and looks correct in every dump. On a driver whose activator targets are
            // alias properties this is the difference between a new mission and a second copy of
            // the old one. A count of zero is a refusal, not a shrug.
            int repointed = gen_delvegatetest.RepointSelfLinks(clone.VirtualMachineAdapter, sourceKey, clone.FormKey);
            Console.WriteLine("  clone    : " + r.id + " " + clone.FormKey + "  self-links repointed: " + repointed);
            if (repointed == 0) { Console.WriteLine("REFUSED: expected at least one self-reference and found none."); return 1; }
            // ⛔ AND EVERY OTHER LINK TO THE BASE, not only the scripts'. A condition is a link too: MB28a's stage 100 picks
            // its completing log entry on GetStageDone(<itself>, 10), and the copy kept testing MB28a, which never runs, so
            // the boss died, stage 100 took the entry that completes nothing, and the board mission never finished (his
            // play of duo_delve_missionboard01a, 2026-10-14). The slate base carried no such condition, so it never showed.
            int stray = clone.EnumerateFormLinks().Count(l => l.FormKey == sourceKey);
            clone.RemapLinks(new Dictionary<FormKey, FormKey> { [sourceKey] = clone.FormKey });
            Console.WriteLine($"  self     : {stray} further link(s) to the base (conditions) now point at the clone");

            int fail = 0;
            var made4 = new BuildMade();
            var madeBeats = new BeatsMade();
            if (t.borrowFrom != null) fail += BorrowAliases(myMod, clone, t, parts);
            if (fail == 0 && r.place.remote == true) fail += MakePlaceRemote(myMod, clone, t, t.placeAlias, parts, r.place);
            // No planet guard on the second request, ever: two crashed the game on start (10a, 12a). See Place.remote.
            if (fail == 0 && r.place.second?.remote == true) fail += MakePlaceRemote(myMod, clone, t, t.secondPlaceAlias, parts, r.place.second!);
            if (fail == 0 && t.secondPlanet != null && r.beats.Any(b => b.PlaceIndex == 1) && (IsBoard(t) || r.place.second?.remote == true))
            {
                fail += MakeSecondPlanet(clone, t);
                madeBeats.SecondPlanet = t.secondPlanet.id;
            }
            if (r.beats.Any(b => b.PlaceIndex == 2))
            {
                made4.ThirdPlaceAlias = IsOrbit(r) ? CreateOrbitPlace(clone, t, parts, made4, OrbitScopes[r.place.third!.orbit!], r.place.third.parsecs) : CreateThirdPlace(clone, t);
                if (made4.ThirdPlaceAlias < 0) fail++;
                else if (IsOrbit(r)) madeBeats.Orbit = (made4.ThirdPlaceAlias, made4.OrbitCell, t.placeAlias, OrbitScopes[r.place.third!.orbit!]);
            }
            foreach (int pi in PlacesUsed(r))
            {
                if (fail > 0) break;
                if (pi == 2 && IsOrbit(r)) continue;   // an orbit's conditions are CreateOrbitPlace's, not a theme's
                var theme = ThemeOf(r, pi);
                fail += WritePlaceConditions(clone, PlaceAliasOf(t, pi, made4.ThirdPlaceAlias), MarkersAt(r, markers, pi),
                                             ResolveKeywords(env, theme.require, _ => { }),
                                             ResolveKeywords(env, theme.exclude, _ => { }));
            }

            lap.Mark("inputs+beats+prose");
            // Everything BuildBeats reads off the load order, own-or-master, in one record (gen_delve_inputs.cs).
            fail = ResolveInputs(myMod, env, t, r, parts, fail, out var inputs);
            if (fail == 0) fail += BuildBeats(myMod, clone, t, r, markers, made4, madeBeats, inputs);
            FormKey? reach = null;
            if (r.place.reach != null)
            {
                var rg = OwnOrMaster<IGlobalGetter>(myMod, env, r.place.reach);
                if (rg == null) { Console.WriteLine($"REFUSED: place.reach '{r.place.reach}' is not a Global in {t.mod} or one of its masters."); fail++; }
                else reach = rg.FormKey;
            }
            if (fail == 0) fail += LeashPlaces(clone, madeBeats, reach, r.place.second?.remote == true, IsOrbit(r));
            fail += WriteProse(clone, t, r);
            SortStages(clone);
            fail += OrderAliases(clone);
            if (fail == 0) fail += EmitModel(myMod, ref clone, r, opts, dry);
            if (fail > 0) { Console.WriteLine("\n=== " + fail + " problem(s). NOTHING WRITTEN. ==="); return 1; }

            lap.Mark("write");
            if (dry && opts.EmitDir != null)
            {
                // The A/B proof's output: the plugin exactly as this build would write it, to a folder that is never the
                // game's. Two builds from the same plugin state allocate the same FormKeys, so their files compare by bytes.
                foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
                string outEsm = Path.Combine(opts.EmitDir, t.mod + ".esm");
                myMod.WriteToBinary(outEsm, gen_quest_main.BuildWriteParams());
                Console.WriteLine($"\n  --dry --emit: wrote {outEsm} ({new FileInfo(outEsm).Length:N0} B) and the model; the live plugin is untouched.");
                return 0;
            }
            if (dry) { Console.WriteLine("\n  --dry: nothing written."); return 0; }

            foreach (var rec in myMod.EnumerateMajorRecords()) rec.IsCompressed = false;
            env.Dispose();   // release the memory-mapped load order before overwriting one of its files
            myMod.WriteToBinary(modFile, gen_quest_main.BuildWriteParams());
            Console.WriteLine("\n  wrote " + modFile + " (" + new FileInfo(modFile).Length.ToString("N0") + " B)");

            lap.Mark("verify");
            int v = VerifyBeats(modFile, readParams, r, t, madeBeats, mastersBefore);
            v += VerifyDeal(modFile, readParams, r);
            lap.Mark("voice");
            // The audio last, and only over a verified plugin: its file names are the written topics' ids.
            return v == 0 ? DeployVoice(r, t, madeBeats) : v;
        }

        /// <summary>
        /// The pools, re-read OFF DISK: the quest sits in exactly the node it names (once) or in none, and no node
        /// in the plugin points at a quest of the plugin's own that no longer exists (the class a rebuild creates).
        /// </summary>
        private static int VerifyDeal(string modFile, Mutagen.Bethesda.Plugins.Binary.Parameters.BinaryReadParameters readParams, Recipe r)
        {
            var reread = StarfieldMod.CreateFromBinaryOverlay(modFile, StarfieldRelease.Starfield, readParams);
            var q = reread.Quests.FirstOrDefault(x => x.EditorID == r.id);
            if (q == null) return 0;   // Verify has already failed on this
            var holding = reread.StoryManagerQuestNodes
                .Select(n => (n.EditorID, count: n.Quests.Count(e => e.Quest.FormKey == q.FormKey))).Where(x => x.count > 0).ToList();
            int fail = Check("the quest is dealt from exactly the node the recipe names",
                             holding.Count == 0 ? "(none)" : string.Join(", ", holding.Select(h => h.count == 1 ? h.EditorID : $"{h.EditorID} x{h.count}")),
                             r.deal ?? "(none)");
            var own = reread.Quests.Select(x => x.FormKey).ToHashSet();
            var dangling = reread.StoryManagerQuestNodes
                .SelectMany(n => n.Quests.Where(e => e.Quest.FormKey.ModKey == reread.ModKey && !own.Contains(e.Quest.FormKey))
                                         .Select(e => $"{n.EditorID} -> {e.Quest.FormKey}")).ToList();
            fail += Check("no story-manager entry points at a quest the plugin no longer has",
                          dangling.Count == 0 ? "none" : string.Join("; ", dangling), "none");
            return fail;
        }

        // ------------------------------------------------------------------ build helpers

        /// <summary>
        /// What the build created before BuildBeats runs: only the third place now. (It carried the retired choice
        /// kind's buyer, people and reward too; what is left is one field, and the split moves it onto the model.)
        /// </summary>
        private sealed class BuildMade
        {
            /// <summary>The created third place's alias id, -1 when the recipe has none.</summary>
            public int ThirdPlaceAlias = -1;
            /// <summary>An orbit third place's space-cell member alias id (its markers fill from it), -1 when not an orbit.</summary>
            public int OrbitCell = -1;
            /// <summary>orbitFrom's marker alias, the pattern an orbit beat's marker takes its from-cell condition off.</summary>
            public QuestReferenceAlias? OrbitMarker;
            /// <summary>An orbit's map marker alias id (filled from the cell; its link and script are set on the orbit beat), -1 when none.</summary>
            public int OrbitMapMarker = -1;
        }

        /// <summary>
        /// GIVE ONE CREATED ACTIVATOR A DIFFERENT MESH, ON A CLONE. The base's activator is shared by his
        /// shipped cargo quests, so it is never edited: it is duplicated as {edid}, Model.File is written
        /// and NOTHING ELSE (gen_setmodel's rule: dropping LightLayer once made thirteen parts invisible),
        /// LightLayer and Flags are asserted unchanged, and the alias is pointed at the clone. Absent path
        /// = nothing happens and the base's model stands. Any previous clone of this name is removed first.
        /// </summary>
        private static int ReskinActivator(StarfieldMod myMod, Quest clone, Template t, string edid, int aliasId, string? path, string? name)
        {
            foreach (var k in myMod.Activators.Where(a => a.EditorID == edid).Select(a => a.FormKey).ToList())
                myMod.Activators.Remove(k);
            if (string.IsNullOrWhiteSpace(path)) return 0;
            var alias = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)aliasId);
            var key = alias?.CreateReferenceToObject?.Object.FormKey;
            var src = key == null ? null : myMod.Activators.FirstOrDefault(a => a.FormKey == key.Value);
            if (src?.Model == null)
            { Console.WriteLine($"REFUSED: alias {aliasId} does not create an activator of {t.mod} with a model to clone."); return 1; }
            var act = myMod.Activators.DuplicateInAsNewRecord(src);
            act.EditorID = edid;
            var ll = act.Model!.LightLayer; var fl = act.Model.Flags;
            act.Model.File = new Mutagen.Bethesda.Plugins.Assets.AssetLink<
                Mutagen.Bethesda.Starfield.Assets.StarfieldModelAssetType>(path);
            if (act.Model.LightLayer != ll || act.Model.Flags != fl)
            { Console.WriteLine("REFUSED: writing Model.File disturbed LightLayer or Flags."); return 1; }
            if (!string.IsNullOrWhiteSpace(name)) act.Name = name;
            alias!.CreateReferenceToObject!.Object.SetTo(act.FormKey);
            Console.WriteLine($"  +reskin  : {edid} {act.FormKey}  model {path}  (clone of {src.EditorID}, alias {aliasId})");
            return 0;
        }

        /// <summary>
        /// The pausing box every Delve message is cloned from: a MessageBox Overtime already ships
        /// (patch 04, 2026-07-31), so its flags are ones the game demonstrably shows.
        /// </summary>
        private const string MessageTemplate = "duo_alocal01_msg03";

        /// <summary>
        /// A record this plugin may LINK to: one in the plugin itself or in one of its masters, found by
        /// EditorID. Never the load order's winner by name, because a plugin that is not a master (his CK
        /// bridge, du_overtime.esp) can carry a same-named copy, and linking to it makes it a master.
        /// </summary>
        private static T? OwnOrMaster<T>(StarfieldMod myMod, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string edid)
            where T : class, IMajorRecordGetter
            => OwnOrMaster<T>(new HashSet<ModKey>(myMod.ModHeader.MasterReferences.Select(m => m.Master)) { myMod.ModKey }, env, edid);

        private static T? OwnOrMaster<T>(HashSet<ModKey> allowed, IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, string edid)
            where T : class, IMajorRecordGetter
            => env.LoadOrder.PriorityOrder.WinningOverrides<T>()
                .FirstOrDefault(x => allowed.Contains(x.FormKey.ModKey) && string.Equals(x.EditorID, edid, StringComparison.OrdinalIgnoreCase));

        /// <summary>The template's mod and its masters, read off the load order (the lint has no mod object of its own).</summary>
        private static HashSet<ModKey> Allowed(IGameEnvironment<IStarfieldMod, IStarfieldModGetter> env, Template t)
        {
            var key = ModKey.FromNameAndExtension(t.mod + ".esm");
            var mod = env.LoadOrder.ListedOrder.FirstOrDefault(l => l.ModKey == key)?.Mod;
            var set = new HashSet<ModKey> { key };
            if (mod != null) foreach (var m in mod.ModHeader.MasterReferences) set.Add(m.Master);
            return set;
        }

        /// <summary>
        /// CREATE THE THIRD PLACE by cloning one of the base's location aliases. The first location alias
        /// this tool has ever created (2026-10-08, the medal Delve, his "third place, time to learn").
        ///
        /// ⭐ A CLONE, NEVER A CONSTRUCTION, for the reason every created thing here is a clone: a
        /// QuestLocationAlias carries flags and fields this tool has no opinion about (the source's flags
        /// read 0x40000101 and its ParentSystemLocationAliasID -2, neither of them understood), and copying
        /// one that demonstrably fills brings them along. It also carries the source's GetDistance leash,
        /// which is the one condition WritePlaceConditions preserves, so the two delivery places share a
        /// leash: the closest thing to "both endings are about the same drive" the records can say.
        /// Its marker and theme conditions are then rebuilt from the recipe like any other place's.
        ///
        /// ⚠ What is NOT established, named rather than assumed: that the story manager fills a quest's
        /// location aliases independently, so that two aliases with disjoint pools always land on two
        /// different POIs. The disjointness is measured at lint; the fill is his playthrough.
        /// </summary>
        private static int CreateThirdPlace(Quest clone, Template t)
        {
            var src = clone.Aliases?.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)(t.thirdPlace?.cloneOf ?? -1));
            if (src == null || string.IsNullOrWhiteSpace(t.thirdPlace?.name))
            { Console.WriteLine($"REFUSED: template '{t.id}' names no location alias {t.thirdPlace?.cloneOf} to clone a third place from, or no name for it."); return -1; }
            uint id = NextAliasId(clone);
            var loc = src.DeepCopy();
            loc.ID = id;
            loc.Name = t.thirdPlace!.name;
            clone.Aliases.Add(loc);
            Console.WriteLine($"  +place   : THIRD place alias {id} \"{loc.Name}\" (clone of location alias {src.ID} \"{src.Name}\", "
                              + $"{loc.Conditions?.Count ?? 0} condition(s) carried before the rebuild)");
            return (int)id;
        }

        /// <summary>LocTypeMajorOrbital: a planet's or a moon's location, the parent of its orbit and its surface.</summary>
        private static readonly FormKey MajorOrbital = FormKey.Factory("070A54:Starfield.esm");

        /// <summary>
        /// THE ORBIT AS THE THIRD PLACE (place.third.orbit, 2026-10-22, his "go on 19a, try over"). orbitFrom's chain off the
        /// parts file: its LocTypeOrbit location (flags and ALFG altitude kept) with its conditions REPLACED by
        /// IsInSameCurrentLocationAsAlias(the main place, LocTypeMajorOrbital) and its own LocTypeSENotAllowed == 0, then the
        /// levelled space cell created at it. Each beat's marker fills from that cell in WriteBeat.
        /// WHY THAT CONDITION: an orbit location and the surface are both children of the planet's location, which carries
        /// LocTypeMajorOrbital (SAlphaCentauri_PJemison_Orbit and _Surface, read 2026-10-22), so "inside the same major
        /// orbital as the main place" is the orbit over its planet, or its moon's. ⚠ NO VANILLA ORBIT ALIAS DOES THIS (0 of
        /// 1,060 measured; 75 use the condition on other kinds of alias), so it is proven by his play or not at all.
        /// </summary>
        private static int CreateOrbitPlace(Quest clone, Template t, Parts? parts, BuildMade made, FormKey scope, double? parsecs)
        {
            if (parts == null || parts.orbit.Count == 0 || t.orbitAliases == null)
            { Console.WriteLine($"REFUSED: an orbit is copied off the parts file and template '{t.id}' has no orbit parts; `gen_delve shell {t.id}` writes them."); return -1; }
            var pool = parts.orbit.Select(gen_questmodel.WriteAliasModel).ToList();
            var o = t.orbitAliases;
            var srcLoc = pool.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)o.place);
            var srcCell = pool.OfType<QuestCollectionAlias>().FirstOrDefault(c => c.Collection.Count == 1 && c.Collection[0].ReferenceAlias?.ID == (uint)o.cell);
            var srcMarker = pool.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)o.marker);
            if (srcLoc == null || srcCell == null || srcMarker == null)
            { Console.WriteLine($"REFUSED: the orbit parts lack the place {o.place}, the cell {o.cell} or the marker {o.marker} the template names."); return -1; }
            var sen = srcLoc.Conditions.FirstOrDefault(c => c.Data is LocationHasKeywordConditionData);
            if (sen == null) { Console.WriteLine($"REFUSED: {parts.orbitFrom}'s orbit carries no LocationHasKeyword condition (LocTypeSENotAllowed) to keep."); return -1; }

            uint locId = NextAliasId(clone);
            var loc = srcLoc.DeepCopy(); loc.ID = locId; loc.Name = "OrbitLocation";
            if (scope.IsNull)
            {
                // "near": the source's conditions stay as copied; only the parsec limit becomes the recipe's number.
                int pi = loc.Conditions.FindIndex(c => c.Data is IGetDistanceGalacticParsecConditionDataGetter);
                if (pi < 0) { Console.WriteLine($"REFUSED: {parts.orbitFrom}'s orbit carries no GetDistanceGalacticParsec condition for \"near\" to set."); return -1; }
                var old = loc.Conditions[pi];
                loc.Conditions[pi] = new ConditionFloat { CompareOperator = old.CompareOperator, Flags = old.Flags, Data = old.Data, ComparisonValue = (float)parsecs!.Value };
                Console.WriteLine($"  orbit    : {parts.orbitFrom}'s own conditions kept, GetDistanceGalacticParsec {old.CompareOperator} {parsecs} (was a Global)");
            }
            else
            {
                var same = new IsInSameCurrentLocationAsAliasConditionData { FirstParameter = t.placeAlias, Unknown3 = -1 };
                same.SecondParameter.Link.SetTo(scope);
                loc.Conditions.Clear();
                loc.Conditions.Add(new ConditionFloat { Data = same, CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f });
                loc.Conditions.Add(sen.DeepCopy());
            }
            clone.Aliases.Add(loc);

            uint cellId = NextAliasId(clone);
            var cell = srcCell.DeepCopy();
            var member = cell.Collection[0].ReferenceAlias!;
            // The entry's id moves WITH its inner alias, as EmptyCollection does. Left at MB01's 10 on 2026-10-22 it
            // collided with FinalLocation (10) while the markers pointed at the new id, and 19a would not start on any map.
            if (cell.Collection[0].ID == member.ID) cell.Collection[0].ID = cellId;
            member.ID = cellId;
            if (member.CreateReferenceToObject == null) { Console.WriteLine($"REFUSED: {parts.orbitFrom}'s cell alias {o.cell} is not CREATED at its orbit."); return -1; }
            member.CreateReferenceToObject.AliasID = (short)locId;
            clone.Aliases.Add(cell);

            // THE MAP MARKER (2026-10-22, his "I feel like other quests use the map marker in space"): 896 of the 903 quests in
            // his load order that create a space cell fill one from it, and every destination mission's links to the target
            // with MapMarkerLinkedRefNameOverride, so the star map names the thing. Its link is set to the orbit beat's object
            // when that beat is filled (Fills), and its DefaultAliasMapMarkerScript there too.
            if (o.mapMarker >= 0)
            {
                var srcMap = pool.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)o.mapMarker);
                if (srcMap == null || srcMap.LinkedAliases.Count != 1)
                { Console.WriteLine($"REFUSED: the orbit parts lack the map marker {o.mapMarker}, or it does not carry exactly one link to its target."); return -1; }
                uint mmId = NextAliasId(clone);
                var mm = srcMap.DeepCopy(); mm.ID = mmId; mm.ReferenceCollectionAliasID = (int)cellId;
                clone.Aliases.Add(mm);
                made.OrbitMapMarker = (int)mmId;
            }
            made.OrbitCell = (int)cellId;
            made.OrbitMarker = srcMarker;
            float alt = BitConverter.Int32BitsToSingle(loc.ALFG ?? 0);
            Console.WriteLine($"  +place   : THIRD place is an ORBIT, alias {locId} \"{loc.Name}\" (off {parts.orbitFrom} alias {o.place}, altitude {alt:0.###}): "
                              + $"inside the same {(scope == MajorOrbital ? "LocTypeMajorOrbital" : "LocTypeStarSystem")} as the main place (alias {t.placeAlias}); space cell {cellId} created at it");
            return (int)locId;
        }

        /// <summary>
        /// Copy the template's borrowAliases in from borrowFrom, renumbered, every alias link inside each copy remapped: through
        /// the list, or the source's main place to this template's. A link to anything else is refused rather than left
        /// pointing at whatever this base happens to number the same (the board's 2 is a planet; the slate's 2 an activator).
        /// </summary>
        private static int BorrowAliases(StarfieldMod myMod, Quest clone, Template t, Parts? parts)
        {
            // From the parts file when the build owns its quest (true construction); off borrowFrom on the old clone path.
            List<AQuestAlias> pool;
            string srcName;
            if (parts != null)
            {
                pool = parts.borrowed.Select(gen_questmodel.WriteAliasModel).ToList();
                srcName = parts.borrowFrom ?? t.borrowFrom ?? "";
            }
            else
            {
                var src = myMod.Quests.FirstOrDefault(q => q.EditorID == t.borrowFrom);
                if (src?.Aliases == null) { Console.WriteLine($"REFUSED: borrowFrom '{t.borrowFrom}' is not a quest in {t.mod}."); return 1; }
                pool = src.Aliases.ToList();
                srcName = src.EditorID ?? "";
            }
            var map = t.borrowAliases.ToDictionary(b => b.from, b => b.to);
            if (t.borrowPlaceAlias >= 0) map[t.borrowPlaceAlias] = t.placeAlias;
            int? Map(int from) => map.TryGetValue(from, out var to) ? to : null;
            foreach (var b in t.borrowAliases)
            {
                if (AliasTaken(clone, (uint)b.to))
                { Console.WriteLine($"REFUSED: alias {b.to} is already taken on this base; borrowed alias {b.from} cannot land there."); return 1; }
                AQuestAlias copy;
                string links = "";
                string name;
                if (pool.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)b.from) is QuestReferenceAlias ra)
                {
                    var c = ra.DeepCopy(); c.ID = (uint)b.to; name = c.Name ?? "";
                    if (c.Location != null)
                    {
                        if (c.Location.AliasID is not int lid || Map(lid) is not int to) { Console.WriteLine($"REFUSED: borrowed alias {b.from} fills from alias {c.Location.AliasID}, which nothing maps."); return 1; }
                        links += $" fills from {c.Location.AliasID}->{to}"; c.Location.AliasID = to;
                    }
                    if (c.CreateReferenceToObject != null)
                    {
                        if (Map(c.CreateReferenceToObject.AliasID) is not int to) { Console.WriteLine($"REFUSED: borrowed alias {b.from} is created at alias {c.CreateReferenceToObject.AliasID}, which nothing maps."); return 1; }
                        links += $" created at {c.CreateReferenceToObject.AliasID}->{to}"; c.CreateReferenceToObject.AliasID = (short)to;
                    }
                    copy = c;
                }
                else if (pool.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)b.from) is QuestLocationAlias la)
                {
                    var c = la.DeepCopy(); c.ID = (uint)b.to; name = c.Name ?? "";
                    foreach (var cd in c.Conditions)
                        if (cd.Data is GetDistanceConditionData gd && gd.UseAliases)
                        {
                            if (Map((int)gd.FirstParameter.Index) is not int to) { Console.WriteLine($"REFUSED: borrowed alias {b.from}'s leash measures to alias {gd.FirstParameter.Index}, which nothing maps."); return 1; }
                            links += $" leash {gd.FirstParameter.Index}->{to}"; gd.FirstParameter.Index = (uint)to;
                        }
                    copy = c;
                }
                else { Console.WriteLine($"REFUSED: {srcName} has no ref or location alias {b.from} to borrow."); return 1; }
                clone.Aliases!.Add(copy);
                Console.WriteLine($"  +borrow  : alias {b.from} -> {b.to} {name} from {srcName}{links}");
            }
            return 0;
        }

        /// <summary>
        /// The second place's planet: a copy of the base's planet alias (secondPlanet.cloneOf) with the second place as its
        /// parent, as every vanilla MB_Cargo resolves its planets (ParentSystemLocationAliasID on a location alias).
        /// </summary>
        private static int MakeSecondPlanet(Quest clone, Template t)
        {
            var sp = t.secondPlanet!;
            var from = clone.Aliases!.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)sp.cloneOf);
            if (from == null) { Console.WriteLine($"REFUSED: the base has no planet alias {sp.cloneOf} to copy the second planet from."); return 1; }
            if (AliasTaken(clone, (uint)sp.id)) { Console.WriteLine($"REFUSED: alias {sp.id} is already taken; the second planet cannot land there."); return 1; }
            var c = from.DeepCopy(); c.ID = (uint)sp.id; c.Name = sp.name; c.ParentSystemLocationAliasID = t.secondPlaceAlias;
            clone.Aliases.Add(c);
            Console.WriteLine($"  +planet  : alias {sp.id} {sp.name}, the planet of the second place (alias {t.secondPlaceAlias}), a copy of {from.Name}");
            return 0;
        }

        /// <summary>
        /// A REMOTE main place (place.remote): the clone's place alias is REPLACED by a deep copy of the template's
        /// remotePlaceFrom quest's alias of the same ID, in the same list position, so the request (ALPS,
        /// PCM_QuestGeneral and its planet condition), its flags and its GetDistanceGalacticParsec limit all come
        /// from a quest that already ships remote, and nothing is constructed. The local base's GetDistance leash
        /// to the player goes with the old alias, which is the point: a remote site is not near him.
        /// WritePlaceConditions then re-emits markers and theme over it like any other place.
        /// ⛔ Own mod only, never a winning override (the esp bridge carries a copy of every duo_ record, and a
        /// link into it adds the esp to the esm's masters: 2026-10-08).
        /// </summary>
        private static int MakePlaceRemote(StarfieldMod myMod, Quest clone, Template t, int target, Parts? parts, Place shape)
        {
            // The request is always the source's MAIN place alias; a second request is that same alias copied over the
            // second place, keeping the second place's ID and name so <SecondPlace> and everything filled from it still bind.
            // A board's own main place IS a request (MB28a's), so its second request copies that, off the clone itself.
            // remotePlaceFrom's comes from the parts file when the build owns its quest, off the quest on the old clone path.
            QuestLocationAlias? from;
            string srcName;
            if (t.remotePlaceFrom != null && parts != null)
            {
                if (parts.remotePlace == null || parts.remotePlace.ID != (uint)t.placeAlias)
                { Console.WriteLine($"REFUSED: {t.parts} carries no remote request at alias {t.placeAlias}; `gen_delve shell {t.id}` rewrites it."); return 1; }
                from = gen_questmodel.WriteAliasModel(parts.remotePlace) as QuestLocationAlias;
                srcName = t.remotePlaceFrom;
            }
            else
            {
                Quest? src = t.remotePlaceFrom != null ? myMod.Quests.FirstOrDefault(q => q.EditorID == t.remotePlaceFrom)
                           : IsBoard(t) ? clone : null;
                if (src == null) { Console.WriteLine($"REFUSED: remotePlaceFrom '{t.remotePlaceFrom}' is not a quest in {t.mod}, and this is not a board (whose own place is a request)."); return 1; }
                from = src.Aliases?.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)t.placeAlias);
                srcName = src.EditorID ?? "";
            }
            if (from?.ALPS == null) { Console.WriteLine($"REFUSED: {srcName} alias {t.placeAlias} is not a PCM request (no ALPS)."); return 1; }
            int at = clone.Aliases!.FindIndex(a => a is QuestLocationAlias l && l.ID == (uint)target);
            if (at < 0) { Console.WriteLine($"REFUSED: the clone has no place alias {target}."); return 1; }
            var old = (QuestLocationAlias)clone.Aliases[at];
            var copy = from.DeepCopy();
            if (target == t.placeAlias && copy.Name != old.Name) { Console.WriteLine($"REFUSED: {srcName} alias {t.placeAlias} is '{copy.Name}' and the base's is '{old.Name}'; the <Place> token would bind to a different name."); return 1; }
            copy.ID = (uint)target;
            copy.Name = old.Name;
            clone.Aliases[at] = copy;
            Console.WriteLine($"  remote   : place alias {target} {copy.Name} is now {srcName}'s PCM request "
                              + $"({(copy.ALPS!.PcmTypeKeyword.IsNull ? "no type" : copy.ALPS.PcmTypeKeyword.FormKey.ToString())}), flags {copy.Flags}");
            return ShapeRequest(copy, shape);
        }

        /// <summary>
        /// A request's two recipe dials, applied to the copied alias (his 2026-10-22, 19a). parsecs: the
        /// GetDistanceGalacticParsec condition keeps its function and subject and compares to a number instead of the
        /// copied Global. habitability false: the ALPS conditions (the request's planet condition) are cleared.
        /// Each refuses when the copied request has nothing to change, so a dial can never be silently a no-op.
        /// </summary>
        private static int ShapeRequest(QuestLocationAlias copy, Place shape)
        {
            if (shape.parsecs is double ps)
            {
                int i = copy.Conditions.FindIndex(c => c.Data is IGetDistanceGalacticParsecConditionDataGetter);
                if (i < 0) { Console.WriteLine($"REFUSED: parsecs is set and request {copy.Name} has no GetDistanceGalacticParsec condition."); return 1; }
                var old = copy.Conditions[i];
                copy.Conditions[i] = new ConditionFloat { CompareOperator = old.CompareOperator, Flags = old.Flags, Data = old.Data, ComparisonValue = (float)ps };
                Console.WriteLine($"  request  : {copy.Name} GetDistanceGalacticParsec {old.CompareOperator} {ps} (was a Global)");
            }
            if (shape.habitability == false)
            {
                int n = copy.ALPS!.Conditions.Count;
                if (n == 0) { Console.WriteLine($"REFUSED: habitability is false and request {copy.Name} has no planet condition to drop."); return 1; }
                copy.ALPS.Conditions.Clear();
                Console.WriteLine($"  request  : {copy.Name} planet condition dropped ({n} ALPS condition(s))");
            }
            return 0;
        }

        /// <summary>
        /// The place's conditions are REBUILT from the recipe, never patched.
        ///
        /// ⭐ Every LocationHasRefType and LocationHasKeyword is dropped and re-emitted from the beat
        /// markers and the theme predicate. Everything else on the list -- in practice the base's
        /// GetDistance leash -- is preserved verbatim, because it is the one condition type this tool
        /// has not proven it can construct.
        ///
        /// That is why an author cannot desynchronise the selection from the beats: there is no field
        /// in which to say something different.
        /// </summary>
        private static int WritePlaceConditions(Quest clone, int placeAlias,
                                                Dictionary<string, FormKey> markers,
                                                List<FormKey> require, List<FormKey> exclude)
        {
            var loc = clone.Aliases?.OfType<QuestLocationAlias>().FirstOrDefault(a => a.ID == (uint)placeAlias);
            if (loc?.Conditions == null) { Console.WriteLine("REFUSED: place alias " + placeAlias + " has no conditions."); return 1; }

            var kept = loc.Conditions.Where(c => c.Data is not ILocationHasRefTypeConditionDataGetter
                                              && c.Data is not ILocationHasKeywordConditionDataGetter).ToList();
            int dropped = loc.Conditions.Count - kept.Count;
            loc.Conditions.Clear();

            foreach (var m in markers)
            {
                var d = new LocationHasRefTypeConditionData();
                d.FirstParameter = new FormLinkOrIndex<ILocationReferenceTypeGetter>(d, m.Value);
                loc.Conditions.Add(new ConditionFloat { Data = d, CompareOperator = CompareOperator.EqualTo, ComparisonValue = 1f });
            }
            foreach (var (k, want) in require.Select(k => (k, 1f)).Concat(exclude.Select(k => (k, 0f))))
            {
                var d = new LocationHasKeywordConditionData();
                d.FirstParameter = new FormLinkOrIndex<IKeywordGetter>(d, k);
                loc.Conditions.Add(new ConditionFloat { Data = d, CompareOperator = CompareOperator.EqualTo, ComparisonValue = want });
            }
            foreach (var c in kept) loc.Conditions.Add(c);

            Console.WriteLine($"  place {placeAlias,-2} : {markers.Count} marker + {require.Count} require + {exclude.Count} exclude, "
                              + $"{kept.Count} preserved (dropped {dropped} authored by the base)");
            return 0;
        }

        /// <summary>
        /// CREATE A BEAT SLOT: a marker alias that fills inside the place, an activator create-obj'd
        /// at it, and a stage to carry the beat's journal line.
        ///
        /// ⭐ IT CLONES THE TEMPLATE'S FIRST SLOT RATHER THAN CONSTRUCTING FROM NOTHING, and that is
        /// the whole safety argument. A QuestReferenceAlias carries flags this tool has no business
        /// having an opinion about -- slot 0's activator is QuestObject | StoresText | CreateRefTemp,
        /// and a fresh object would have none of them. Copying a slot that demonstrably works in a
        /// shipped quest brings along every field I do not know exists, which is the same argument
        /// that made the layer-1 clone safe. Only ID, Name, RefType and the create-at target move.
        ///
        /// ⚠ His own warning on this operation, and it is why extras are built as a UNIT rather than
        /// one alias at a time: "aliases freak out if they are wrong it's better to build in layers."
        /// A half-made slot -- a marker with no activator, or an activator pointing at an alias that
        /// does not exist -- is an unfillable non-optional alias, and a mission with one of those
        /// SILENTLY DOES NOT LOAD. So this returns null and refuses rather than leaving a partial.
        /// </summary>
        private static BeatSlot? AddBeatSlot(Quest q, Template t, int index, int stage)
        {
            var src = t.beatSlots[0];
            var srcMarker = q.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)src.markerAlias);
            var srcAct = q.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)src.activatorAlias);
            if (srcMarker?.Location == null || srcAct?.CreateReferenceToObject == null)
            { Console.WriteLine("REFUSED: template slot 0 is not a marker+activator pair on this base."); return null; }

            uint next = NextAliasId(q);
            uint mid = next, aid = next + 1;

            var marker = srcMarker.DeepCopy();
            marker.ID = mid;
            marker.Name = "DelveBeat" + index + "Marker";

            var act = srcAct.DeepCopy();
            act.ID = aid;
            act.Name = "DelveBeat" + index + "Target";
            act.CreateReferenceToObject!.AliasID = (short)mid;

            q.Aliases.Add(marker);
            q.Aliases.Add(act);

            // The stage has to exist before anything can set it, and a stage with no log entry
            // renders nothing at all, so the entry is created here rather than left to the prose
            // pass to discover missing.
            if (q.Stages!.Any(s => s.Index == stage))
            { Console.WriteLine($"REFUSED: stage {stage} already exists on this base; extra-beat numbering collides."); return null; }
            var st = CloneStage(q, stage);
            if (st == null) return null;
            q.Stages.Add(st);

            Console.WriteLine($"  +slot    : beat {index + 1} created -- marker alias {mid}, activator {aid}, stage {stage}");
            return new BeatSlot { markerAlias = (int)mid, activatorAlias = (int)aid, objective = -1, journalStage = stage };
        }

        /// <summary>
        /// A NEW STAGE IS A CLONE OF ONE THAT WORKS, never constructed. His xEdit, 2026-09-24: every
        /// created stage was written as INDX + CNAM only, because a fresh QuestLogEntry has no flags and
        /// Mutagen writes nothing for a field that was never set. The base's stages are INDX, QSDT,
        /// CNAM, QSRD, QSRD. xEdit lost its place at the first bare CNAM and read every objective and
        /// alias after it as "unexpected", so the record showed empty. Same argument as AddBeatSlot's
        /// alias clone: copying a working stage brings every field this tool does not know exists.
        /// The source is a plain journal stage that does NOT complete the quest; its text is cleared
        /// so it cannot leak the base's journal line.
        /// </summary>
        private static QuestStage? CloneStage(Quest q, int index)
        {
            var src = q.Stages?.OrderBy(x => x.Index)
                .FirstOrDefault(x => x.Index != 0 && x.LogEntries.Count == 1 && x.LogEntries[0].Flags != null
                                     && !x.LogEntries[0].Flags!.Value.HasFlag(QuestLogEntry.Flag.CompleteQuest));
            if (src == null) { Console.WriteLine("REFUSED: the base has no plain journal stage to clone a new one from."); return null; }
            var st = src.DeepCopy();
            st.Index = (ushort)index;
            st.LogEntries[0].Entry = null;
            return st;
        }

        /// <summary>Stages in ascending index order, as every shipped quest writes them.</summary>
        private static void SortStages(Quest q)
        {
            var sorted = q.Stages!.OrderBy(x => x.Index).ToList();
            q.Stages.Clear();
            q.Stages.AddRange(sorted);
        }

        /// <summary>
        /// PUT EVERY ALIAS BELOW EVERYTHING IT FILLS FROM. The engine fills aliases in list order
        /// (vanilla: 7,731 dependencies across 2,092 quests, ZERO pointing down the list), and a
        /// second place re-opened for beat one sits BELOW beat one's marker in the base's list.
        /// A place's leash anchor counts too (AliasGraph.LeashDepsOf): an anchor that fills inside the
        /// place it leashes is a cycle, so that build is refused rather than written.
        /// Stable: nothing moves unless it has to. Refuses on a cycle rather than guessing, naming the aliases.
        /// </summary>
        private static int OrderAliases(Quest q)
        {
            if (q.Aliases == null) return 0;
            var before = q.Aliases.ToList();
            var fwd = AliasGraph.ForwardRefs(before);
            var result = AliasGraph.DependencyOrder(before);
            if (result.Order is not { } order)
            {
                Console.WriteLine($"REFUSED: aliases {string.Join(", ", result.Unplaced)} could not be placed: a cycle of "
                                  + "fill dependencies is among them, so no fill order exists.");
                return 1;
            }
            int moved = before.Where((a, i) => !ReferenceEquals(order[i], a)).Count();
            q.Aliases.Clear();
            q.Aliases.AddRange(order);
            Console.WriteLine($"  alias order: {fwd.Count} forward reference(s) before, {moved} list position(s) changed"
                              + (fwd.Count > 0 ? "  (" + string.Join(", ", fwd.Select(f => $"{f.alias}->{f.dep}")) + ")" : ""));
            return AliasGraph.ForwardRefs(q.Aliases.ToList()).Count == 0 ? 0 : 1;
        }

        /// <summary>
        /// The id a new alias takes: one past the highest the quest carries. ONE copy (split step 3e, 2026-10-15; it
        /// was spelled nine times). Every alias counts, whatever its name (AliasGraph.IdsOf). Until 2026-10-15 it read
        /// through a Flatten that skipped an alias whose Name was NULL. Mutagen reads a missing name as empty, never
        /// null, so no alias read off a plugin was ever skipped; only code, or a model JSON carrying "Name": null
        /// (gen_questmodel's writer copies it across), could make one. Whether an id is taken has nothing to do with a
        /// name, so the filter went. No output changed (both shells, both parts files and ten built quests measured,
        /// none with a null name); `gen_delve selftest` plants the case.
        /// </summary>
        private static uint NextAliasId(Quest q) => 1 + q.Aliases!.SelectMany(AliasGraph.IdsOf).DefaultIfEmpty(0u).Max();

        /// <summary>Whether an alias of the quest already holds this id, named or not (the same reading as NextAliasId).</summary>
        private static bool AliasTaken(Quest q, uint id) => q.Aliases!.SelectMany(AliasGraph.IdsOf).Contains(id);

        /// <summary>Remove the previous build's records of one group, by their EditorID, before minting this build's: a
        /// rebuild replaces, it never accumulates. ONE copy of what six sites spelled (split step 3e).</summary>
        private static void RemovePrevious<T>(IGroup<T> group, Func<string, bool> ours) where T : class, IStarfieldMajorRecordInternal
        {
            foreach (var k in group.Where(m => m.EditorID != null && ours(m.EditorID)).Select(m => m.FormKey).ToList())
                group.Remove(k);
        }

        /// <summary>A copy of a source collection alias, EMPTY, renumbered and renamed, its packages cleared; null when
        /// the source carries a fill (the caller refuses in its own words). Where vanilla keeps the entry's id equal to
        /// its inner alias's, so does the copy. Hold waves and clear groups both start this way.</summary>
        private static QuestCollectionAlias? EmptyCollection(IQuestCollectionAliasGetter src, uint id, string name)
        {
            var col = src.DeepCopy();
            var entry = col.Collection[0];
            var ra = entry.ReferenceAlias!;
            if (ra.Location != null || !ra.ForcedReference.IsNull || !ra.UniqueActor.IsNull || ra.CreateReferenceToObject != null) return null;
            if (entry.ID == ra.ID) entry.ID = id;
            ra.ID = id;
            ra.Name = name;
            ra.PackageData.Clear();
            return col;
        }

        /// <summary>A stock Default* alias hook's script: StageToSet and PrereqStage, then its own properties, in that order.</summary>
        private static ScriptEntry StageHook(string script, int stage, int prereq, params ScriptProperty[] more)
        {
            var sc = new ScriptEntry { Name = script };
            sc.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = stage, Flags = ScriptProperty.Flag.Edited });
            sc.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = prereq, Flags = ScriptProperty.Flag.Edited });
            sc.Properties.AddRange(more);
            return sc;
        }

        /// <summary>DefaultAliasOnDistanceLessThan: within ApproachDistance of the quest's alias targetAlias, set the stage.
        /// Its properties in the order every distance hook here has always written them.</summary>
        private static ScriptEntry DistanceHook(FormKey quest, int targetAlias, int stage, int prereq)
        {
            var sc = new ScriptEntry { Name = "DefaultAliasOnDistanceLessThan" };
            var ta = new ScriptObjectProperty { Name = "TargetAlias", Flags = ScriptProperty.Flag.Edited };
            ta.Object.SetTo(quest);
            ta.Alias = (short)targetAlias;
            sc.Properties.Add(ta);
            sc.Properties.Add(new ScriptFloatProperty { Name = "TargetDistance", Data = ApproachDistance, Flags = ScriptProperty.Flag.Edited });
            sc.Properties.Add(new ScriptIntProperty { Name = "StageToSet", Data = stage, Flags = ScriptProperty.Flag.Edited });
            sc.Properties.Add(new ScriptIntProperty { Name = "PrereqStage", Data = prereq, Flags = ScriptProperty.Flag.Edited });
            return sc;
        }

        /// <summary>How a hook finds the alias's script entry: a fresh one, the existing one or a fresh one, or only an
        /// existing one.</summary>
        private enum HookEntry { New, FindOrCreate, MustExist }
        private enum Hooked { Added, AlreadyCarries, NoEntry }

        /// <summary>
        /// Put a hook on an alias and record it for the verify. ONE copy of what seven sites each spelled (split step 3e).
        /// ⚠ THE SEVEN DIFFERED, AND STILL DO, ON PURPOSE (his "sounds right", 2026-10-15, to keeping them exact): which
        /// entry they take (<see cref="HookEntry"/>) and whether a second copy of the same script on one alias is refused.
        /// So both are arguments, and the caller refuses in its own words.
        /// </summary>
        private static Hooked AttachHook(QuestAdapter vma, FormKey quest, int alias, ScriptEntry sc, HookEntry how, bool refuseTwice,
                                         BeatsMade bm, int stage, int prereq)
        {
            var entry = how == HookEntry.New ? null : vma.Aliases.FirstOrDefault(a => a.Property.Alias == alias);
            if (entry == null)
            {
                if (how == HookEntry.MustExist) return Hooked.NoEntry;
                entry = NewAliasEntry(vma, quest, alias);
            }
            if (refuseTwice && entry.Scripts.Any(x => x.Name == sc.Name)) return Hooked.AlreadyCarries;
            entry.Scripts.Add(sc);
            bm.Hooks.Add((alias, sc.Name, stage, prereq));
            return Hooked.Added;
        }

        /// <summary>A fresh script entry for an alias on the quest's VMAD, added and returned. Shared by AttachHook and
        /// the outside holder's spawn-near script (BeatsBuild.SpawnNear).</summary>
        private static QuestFragmentAlias NewAliasEntry(QuestAdapter vma, FormKey quest, int alias)
        {
            var entry = new QuestFragmentAlias();
            entry.Property.Object.SetTo(quest);
            entry.Property.Alias = (short)alias;
            vma.Aliases.Add(entry);
            return entry;
        }

        /// <summary>
        /// Point one beat's marker alias at the place, and give the beat its objective and journal.
        /// ⚠ BOTH HALVES OF THE FILL ARE SET EVERY TIME, including the half that is not changing.
        /// "Which marker, inside which drawn place" is one fact; writing only the half that moved is
        /// how a quest ends up selecting a POI on one marker and hunting for another.
        /// </summary>
        private static int WriteBeat(Quest clone, Template t, BeatSlot slot, Beat beat,
                                     Dictionary<string, FormKey> markers, bool created, int placeAlias,
                                     int orbitCell = -1, QuestReferenceAlias? orbitMarker = null)
        {
            var al = clone.Aliases?.OfType<QuestReferenceAlias>().FirstOrDefault(a => a.ID == (uint)slot.markerAlias);
            if (al?.Location == null) { Console.WriteLine("REFUSED: ref alias " + slot.markerAlias + " has no location fill."); return 1; }
            if (orbitCell >= 0)
            {
                // IN ORBIT: the marker is the space cell's own, so it fills FROM the cell's collection by its ref type, as his
                // MB01's SpawnMarker01 does. A created cell's markers are not registered on the orbit location, so an
                // in-location fill would find nothing there.
                if (orbitMarker == null || !orbitMarker.Conditions.Any(c => c.Data is HasRefTypeConditionData))
                { Console.WriteLine("REFUSED: the orbit's marker pattern carries no HasRefType condition to fill a marker from the cell by."); return 1; }
                al.Location = null;
                al.ReferenceCollectionAliasID = orbitCell;
                al.Conditions.Clear();
                foreach (var c in orbitMarker.Conditions)
                {
                    var cc = c.DeepCopy();
                    if (cc.Data is HasRefTypeConditionData h) h.FirstParameter.Link.SetTo(markers[beat.at]);
                    al.Conditions.Add(cc);
                }
            }
            else
            {
                al.Location.AliasID = placeAlias;
                al.Location.RefType.SetTo(markers[beat.at]);
            }

            // A created slot has no objective by construction: the driver displays only its own two,
            // and the lint has already refused any recipe that tried to give one an objective.
            if (!created)
            {
                var ob = clone.Objectives?.FirstOrDefault(o => o.Index == slot.objective);
                if (ob == null) { Console.WriteLine("REFUSED: no objective " + slot.objective + " on the base."); return 1; }
                ob.DisplayText = Expand(beat.objective!, t);
            }

            if (beat.journal != null)
            {
                var e = clone.Stages?.FirstOrDefault(s => s.Index == slot.journalStage)?.LogEntries?.FirstOrDefault();
                if (e == null)
                {
                    Console.WriteLine($"REFUSED: stage {slot.journalStage} has no log entry to carry this beat's journal. "
                                      + "Authoring one is a stage write, not an alias write, and this tool does not do it.");
                    return 1;
                }
                e.Entry = Expand(beat.journal, t);
            }

            Console.WriteLine($"  beat     : alias {slot.markerAlias} -> {beat.at} "
                              + (orbitCell >= 0 ? $"from space cell {orbitCell} (in orbit, place alias {placeAlias})" : $"inside place alias {placeAlias}")
                              + $", objective {slot.objective}"
                              + (beat.journal != null ? $", journal at stage {slot.journalStage}" : ", no journal"));
            return 0;
        }

        private static int WriteProse(Quest clone, Template t, Recipe r)
        {
            clone.Name = Expand(r.prose.name!, t);
            var e = clone.Stages?.FirstOrDefault(s => s.Index == t.briefingStage)?.LogEntries?.FirstOrDefault();
            if (e == null) { Console.WriteLine("REFUSED: no log entry on briefing stage " + t.briefingStage + "."); return 1; }
            e.Entry = Expand(r.prose.briefing!, t);
            Console.WriteLine("  prose    : name + briefing at stage " + t.briefingStage);
            if (IsBoard(t))
            {
                clone.MissionBoardDescription = Expand(r.prose.card!, t);
                Console.WriteLine("  card     : the board's description");
            }
            return 0;
        }

        /// <summary>Map the recipe's portable tokens onto this template's actual alias names.</summary>
        private static string Expand(string s, Template t)
        {
            foreach (var kv in t.tokens) s = s.Replace("<" + kv.Key + ">", "<Alias=" + kv.Value + ">");
            return s;
        }

        // ------------------------------------------------------------------ verification

        private static int Check(string label, string actual, string expected)
        {
            bool ok = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
            Console.WriteLine("    [" + (ok ? "OK  " : "FAIL") + "] " + label + ": got '" + actual + "' expected '" + expected + "'");
            return ok ? 0 : 1;
        }

        // ------------------------------------------------------------------ plumbing

        private static int ListTemplates(List<Template> ts)
        {
            foreach (var t in ts)
            {
                Console.WriteLine();
                Console.WriteLine("  " + t.id);
                Console.WriteLine("    base    : " + t.@base + " (" + t.mod + ")");
                Console.WriteLine("    driver  : " + t.driver);
                Console.WriteLine("    beats   : " + t.beats + (t.carriesItem ? ", carries an item between them" : ""));
                Console.WriteLine("    tokens  : " + string.Join(", ", t.tokens.Keys.Select(k => "<" + k + ">")));
                foreach (var c in t.cannot) Console.WriteLine("    cannot  : " + c);
                Console.WriteLine("    verified: " + t.verifiedFrom);
            }
            return 0;
        }

        private static List<Template>? LoadTemplates(string dataDir, out string? err)
        {
            err = null;
            string f = Path.Combine(dataDir, "templates.json");
            if (!File.Exists(f)) { err = "no templates.json at " + f; return null; }
            try
            {
                string text = File.ReadAllText(f);
                var tf = JsonSerializer.Deserialize<TemplateFile>(text, JsonOpts);
                if (tf == null || tf.templates.Count == 0) { err = f + " holds no templates"; return null; }
                // ⛔ A KEY THE ROW CANNOT HOLD IS REFUSED, as a recipe's is. Retiring a field (collapsedPlaceAliases with the
                // dualactivator kind, 2026-10-15) left it sitting in both rows, read by nothing and refused by nothing, until
                // an edit happened to land beside it. A "_" key is a note.
                var unknown = new List<string>();
                using (var doc = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
                    UnknownKeys(doc.RootElement, typeof(TemplateFile), "", unknown);
                if (unknown.Count > 0) { err = "templates.json has key(s) this tool does not know, so they would be IGNORED: " + string.Join("; ", unknown); return null; }
                if (tf.schema != 1) { err = $"templates.json schema is {tf.schema}, this tool speaks 1"; return null; }
                // A row of a retired kind (dualactivator, choice, delve4) is refused by name, never built down a default path.
                var bad = tf.templates.Where(x => !Kinds.Contains(x.kind)).Select(x => $"{x.id} ('{x.kind}')").ToList();
                if (bad.Count > 0) { err = $"templates.json row(s) of a kind this tool does not build: {string.Join(", ", bad)}. The kinds are {string.Join(", ", Kinds)}."; return null; }
                return tf.templates;
            }
            catch (JsonException ex) { err = "templates.json is not valid JSON -- " + ex.Message; return null; }
        }

        private static IEnumerable<string> CandidateRoots()
        {
            // Walk up from the assembly first (works for a published exe and for `dotnet run`, whose
            // binaries sit under bin/) and only then from the working directory. Printed on failure
            // rather than guessed at, so "it cannot find its data" is never a mystery.
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null) { yield return d.FullName; d = d.Parent; }
            d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null) { yield return d.FullName; d = d.Parent; }
        }

        private static string? ResolveDataDir()
        {
            foreach (var root in CandidateRoots())
            {
                string p = Path.Combine(root, "data", "delves");
                if (Directory.Exists(p)) return p;
            }
            return null;
        }
    }
}
