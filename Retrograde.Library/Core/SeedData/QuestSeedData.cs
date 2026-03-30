using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class QuestSeedData
    {
        public static readonly List<string> InvestigationVerbs = new List<string>
        {
            "Track", "Locate", "Find", "Follow", "Trace", "Pursue", "Uncover"
        };

        public static readonly List<string> ShowdownVerbs = new List<string>
        {
            "Eliminate", "Confront", "Corner", "End", "Settle"
        };

        public static readonly List<string> DiscoveryVerbs = new List<string>
        {
            "Intercept", "Recover", "Retrieve", "Obtain", "Collect"
        };
    }
}
