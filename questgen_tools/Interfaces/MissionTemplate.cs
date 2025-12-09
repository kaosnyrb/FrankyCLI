using FrankyCLI.questgen_quests;
using FrankyCLI.questgen_tools.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class MissionTemplate
    {
        public string Name;
        public string Description;
        public string Location;
        public string parameter1;
        public uint parameterformid;
        public uint formid;
        public bool needSpacesuit;
        public ITemplateManager Lib1;
        public ITemplateManager Lib2;
        public IOutlawQuest outlawQuest;  //This is an interface that wraps the actual quest template implementation
        public List<string> MissionTags;
        public List<string> Addons;
    }
}
