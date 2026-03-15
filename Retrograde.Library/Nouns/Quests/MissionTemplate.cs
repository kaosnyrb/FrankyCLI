using Retrograde.Chains.Interfaces;
using Mutagen.Bethesda.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Chains
{
    public class MissionTemplate
    {
        public string Name;
        public string Description;
        public string Location;

        //Used to configure the differences in missions
        // Keys: "NeedSpacesuit" (bool), "Label" (string), "FormId" (uint), "Faction", "StationSize", "SpaceCell", etc.
        public Dictionary<string, object> parameters;

        public ITemplateManager Lib1;
        public ITemplateManager Lib2;
        public FormKey formid;

        public IOutlawQuest outlawQuest;  //This is an interface that wraps the actual quest template implementation
        public List<string> MissionTags;
        public List<string> Addons;

        /// <summary>
        /// Short description of the NPC's role, personality, and knowledge limits.
        /// Injected into dialogue generation and refinement prompts to shape voice and diction.
        /// Example: "dock manifest clerk, seen too much, says little"
        /// </summary>
        public string NpcBackground = "";
    }
}
