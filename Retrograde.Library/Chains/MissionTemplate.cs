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
        public Dictionary<string, object> parameters;


        //Old Params
        public bool needSpacesuit;
        public ITemplateManager Lib1;
        public ITemplateManager Lib2;
        public string parameter1;
        public uint parameterformid;
        public FormKey formid;


        public IOutlawQuest outlawQuest;  //This is an interface that wraps the actual quest template implementation
        public List<string> MissionTags;
        public List<string> Addons;
    }
}
