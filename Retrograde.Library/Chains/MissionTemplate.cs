using Retrograde.AI.Utils;
using Retrograde.Chains.Interfaces;
using Retrograde.Lore;
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
        /// When set, PromptManager bypasses AI calls for quest name and log entry,
        /// returning the lore-authored values instead.
        /// Set before calling IOutlawQuest.Setup(); clear PromptManager.ActiveLore after Setup() returns.
        /// </summary>
        public QuestLore? Lore
        {
            get => _lore;
            set { _lore = value; PromptManager.ActiveLore = value; }
        }
        private QuestLore? _lore;
    }
}
