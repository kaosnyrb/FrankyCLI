using System.Collections.Generic;

namespace Retrograde.Grammar
{
    public enum Motivation
    {
        Justice
    }

    public enum StageRole
    {
        Discovery,
        Escalation,
        Setback,
        Revelation,
        Resolution
    }

    public enum MoralFraming
    {
        ClearGuilt,
        GuiltyButJustified,
        Framed,
        SympatheticFugitive
    }

    public enum ArcShape
    {
        RagsToRiches,
        Tragedy,
        ManInAHole,
        Icarus,
        Cinderella,
        Oedipus
    }

    public class Strategy
    {
        public string Name;
        public ArcShape Arc;
        public List<StageRole> Stages;
        public List<MoralFraming> ValidFramings;

        /// <summary>
        /// Short description injected into AI context so it understands the strategy's intent.
        /// </summary>
        public string Description;
    }

    public static class GrammarData
    {
        public static readonly List<Strategy> JusticeStrategies = new List<Strategy>
        {
            new Strategy
            {
                Name = "Steady Hunt",
                Arc = ArcShape.RagsToRiches,
                Description = "A clean escalation. Each stage brings the hunter closer to the target. No twists, no setbacks — just steady progress toward a satisfying conclusion.",
                Stages = new List<StageRole>
                {
                    StageRole.Discovery,
                    StageRole.Escalation,
                    StageRole.Escalation,
                    StageRole.Resolution
                },
                ValidFramings = new List<MoralFraming> { MoralFraming.ClearGuilt }
            },
            new Strategy
            {
                Name = "Cold Trail",
                Arc = ArcShape.ManInAHole,
                Description = "The investigation hits a dead end mid-chain. A lead goes cold, an informant lies, or the target slips away. The hunter must recover and find a new path to the showdown.",
                Stages = new List<StageRole>
                {
                    StageRole.Discovery,
                    StageRole.Escalation,
                    StageRole.Setback,
                    StageRole.Revelation,
                    StageRole.Resolution
                },
                ValidFramings = new List<MoralFraming>
                {
                    MoralFraming.ClearGuilt,
                    MoralFraming.GuiltyButJustified
                }
            },
            new Strategy
            {
                Name = "Double Bluff",
                Arc = ArcShape.Oedipus,
                Description = "The target was framed. The real villain is someone the player already encountered. A mid-chain revelation reframes everything the player thought they knew.",
                Stages = new List<StageRole>
                {
                    StageRole.Discovery,
                    StageRole.Escalation,
                    StageRole.Revelation,
                    StageRole.Resolution
                },
                ValidFramings = new List<MoralFraming>
                {
                    MoralFraming.Framed,
                    MoralFraming.SympatheticFugitive
                }
            }
        };

        public static Strategy SelectStrategy(Motivation motivation)
        {
            var pool = motivation switch
            {
                Motivation.Justice => JusticeStrategies,
                _ => JusticeStrategies
            };
            return pool[RandomProvider.Random.Next(pool.Count)];
        }

        public static MoralFraming SelectFraming(Strategy strategy)
        {
            return strategy.ValidFramings[RandomProvider.Random.Next(strategy.ValidFramings.Count)];
        }

        /// <summary>
        /// Human-readable description of a stage role for injection into AI prompts.
        /// </summary>
        public static string DescribeStageRole(StageRole role, MoralFraming framing)
        {
            return role switch
            {
                StageRole.Discovery =>
                    "DISCOVERY — the player receives the initial lead. Set the hook: who is the target, what did they do, where does the trail start.",

                StageRole.Escalation =>
                    "ESCALATION — the investigation deepens. The player uncovers new evidence, follows a lead, or interrogates a source. Each escalation should feel like progress toward the target.",

                StageRole.Setback =>
                    "SETBACK — the lead was wrong, the informant lied, or the target slipped away. The player is further from their goal than they thought. This stage should feel like a step backward — frustration, misdirection, or danger.",

                StageRole.Revelation => framing switch
                {
                    MoralFraming.Framed =>
                        "REVELATION — the player discovers the target was framed. The real villain is someone they already encountered. Everything the player thought they knew is wrong.",
                    MoralFraming.SympatheticFugitive =>
                        "REVELATION — the player discovers the target's true situation. They're running from something worse than justice. The hunt takes on a different moral weight.",
                    MoralFraming.GuiltyButJustified =>
                        "REVELATION — the player learns why the target did what they did. The crime was real, but the reason behind it complicates the picture.",
                    _ =>
                        "REVELATION — the player uncovers a critical truth that reframes the investigation. What seemed straightforward is more complex than it appeared."
                },

                StageRole.Resolution =>
                    "RESOLUTION — the final confrontation. The player faces the target with everything they've learned. The showdown should feel like the logical conclusion of this specific story.",

                _ => ""
            };
        }
    }
}
