using FrankyCLI.questgen_tools;
using FrankyCLI.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    public  class Templates_SpecificDungeons : TemplateLib
    {
        public Templates_SpecificDungeons() {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            //Based on the keywords create a showdown for each.
            foreach (var PCM in gen_quest_main.myMod.Keywords)
            {
                if(PCM.EditorID.Contains("duout_PCM_Request_"))
                {
                    string PCMName = PCM.Name;

                    //-------------------------------  INVESTIGATION ------------------------------------------
                    InvestigationTemplates.Add(new MissionTemplate()
                    {
                        Name = "Dungeon Activator - " + PCMName,
                        Description = "Find a lead to the target at a " + PCMName,
                        Location = PCMName,
                        formid = FormKeyLookup.GetFormKey("duout_info_dungeon_activator_small"),
                        parameterformid = PCM.FormKey.ID,
                        needSpacesuit = true,
                        outlawQuest = new Investigation_ActivatorSetDungeon(),
                        MissionTags = new List<string>()
                        {
                            "follow_clue",
                            "planetside",
                        }
                    });
                    InvestigationTemplates.Add(new MissionTemplate()
                    {
                        Name = "Dungeon Destroy - " + PCMName,
                        Description = "Destroy an object at a " + PCMName,
                        Location = PCMName,
                        formid = FormKeyLookup.GetFormKey("duout_info_dungeon_destroy_small"),
                        parameterformid = PCM.FormKey.ID,
                        needSpacesuit = true,
                        outlawQuest = new Investigation_DestroySetDungeon(),
                        MissionTags = new List<string>()
                        {
                            "destroy_object",
                            "planetside",
                        }
                    });

                    //-------------------------------  SHOWDOWN ------------------------------------------
                    ShowdownTemplates.Add(new MissionTemplate()
                    {
                        Name = "Dungeon Bounty - " + PCMName,
                        Description = "Kill the target who is hiding out at a " + PCMName,
                        Location = PCMName,
                        formid = FormKeyLookup.GetFormKey("duout_show_planet_kill_setdungon_bossloc"),
                        parameterformid = PCM.FormKey.ID,
                        needSpacesuit = true,
                        outlawQuest = new Showdown_BountyPlanet(),
                        MissionTags = new List<string>()
                        {
                            "kill_target",
                            "planetside",
                        }
                    });
                }
            }
        }
    }
}


