using Mutagen.Bethesda.Starfield;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Nouns;
using Retrograde.Writing;

namespace Retrograde.Story
{
    /// <summary>
    /// Generalized beat interface — the atomic unit of a story.
    /// Existing IOutlawQuest implementations are bridged via OutlawQuestAdapter.
    /// New beat types can implement this directly.
    /// </summary>
    public interface IStoryBeat
    {
        Quest Setup(StarfieldMod mod, StoryCast cast, BeatContext context, IStoryBeat? nextBeat);
        string LogMessage { get; set; }
        string QuestLocation { get; set; }
        Quest questform { get; set; }

        IEnumerable<IPolishable> GetPolishables()
        {
            if (questform != null)
                yield return new QuestLogPolishable(questform);
        }

        void StageAudio() { }
    }

    /// <summary>
    /// Everything a beat needs to know about its narrative context.
    /// </summary>
    public class BeatContext
    {
        public MissionTemplate Template;
        public string SchemaId = "";
        public string NarrativeFunction = "";
        public int ProgressPercent;
        public string StageName = "";
        public string PlayerRole = "bounty hunter";
        public List<string> Addons = new();
        public StoryCast Cast;
    }

    /// <summary>
    /// Wraps any existing IOutlawQuest as an IStoryBeat.
    /// The cast's "target" role maps to the OutlawNpc parameter.
    /// </summary>
    public class OutlawQuestAdapter : IStoryBeat
    {
        public readonly IOutlawQuest Inner;

        public OutlawQuestAdapter(IOutlawQuest inner) => Inner = inner;

        public Quest Setup(StarfieldMod mod, StoryCast cast, BeatContext ctx, IStoryBeat? nextBeat)
        {
            var outlawNpc = (OutlawNpc)cast.GetRole("target").NounInstance!;
            var nextInner = (nextBeat as OutlawQuestAdapter)?.Inner;
            return Inner.Setup(mod, outlawNpc, ctx.Template, nextInner!);
        }

        public string LogMessage
        {
            get => Inner.LogMessage;
            set => Inner.LogMessage = value;
        }

        public string QuestLocation
        {
            get => Inner.QuestLocation;
            set => Inner.QuestLocation = value;
        }

        public Quest questform
        {
            get => Inner.questform;
            set => Inner.questform = value;
        }

        public IEnumerable<IPolishable> GetPolishables() => Inner.GetPolishables();
        public void StageAudio() => Inner.StageAudio();
    }
}
