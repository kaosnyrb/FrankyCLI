using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Nouns;
using FrankyCLI.questgen_tools.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    public class Investigation_Informant_Planet : IOutlawQuest
    {
        private Quest questform;

        public string logMessage { get; set; }

        public string LogMessage
        {
            get => logMessage;
            set => logMessage = value;
        }
        Quest IOutlawQuest.questform
        {
            get => questform;
            set => questform = value;
        }

        string questloc { get; set; }
        string IOutlawQuest.QuestLocation { get => questloc; set => questloc = value; }

        public Quest Setup(StarfieldMod myMod, OutlawNpc outlawNpc, MissionTemplate missionTemplate, IOutlawQuest nextQuest)
        {
            Console.WriteLine("Generating Informant Planet Quest...");

            if (missionTemplate.parameters != null)
            {
                if (missionTemplate.parameters.ContainsKey("ExtraLore"))
                {
                    missionTemplate.Addons.Add(missionTemplate.parameters["ExtraLore"].ToString());
                }
            }

            var questActivator = ActivatorTools.GetRandomLargeGroundType();

            var datasource = PromptManager.GetActivatorName(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Type: Data tablet \r\n",

            });
            Console.WriteLine("datasource: " + datasource);

            var questname = PromptManager.GetQuestName(new List<string>(missionTemplate.Addons)
            {
                "Vital clue to their location:" + datasource,
                "Location:" + missionTemplate.Location + "\r\n",
            });

            Console.WriteLine("questname: " + questname);
            IGang outlawGang = GangManager.GetGang();
            
            //Log Entry
            var logmessage = PromptManager.GetLogMessage(new List<string>(missionTemplate.Addons)
            {
                "Location:" + missionTemplate.Location + "\r\n",
                "Objective: Find the " + datasource + " to lead you to " + outlawNpc.name + "\r\n"
            });
            Console.WriteLine("logmessage: " + logmessage);

            var newQuest = new QuestNoun(missionTemplate.formid, questname);
            newQuest.SetLogMessage(0, 0, logmessage);
            newQuest.SetScriptAlias(0, newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "BountyTarget", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "GangMembers", outlawGang.gangList.ToLink<IStarfieldMajorRecordGetter>());

            //Create Boss NPC
            var outfit = NPCTools.GetRandomOutfit(true);
            bool isfemale = false;
            if (RandomUtils.random.Next(100) > 50)
            {
                isfemale = true;
            }

            Npc NPC = myMod.Npcs[new FormKey(myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
            if (missionTemplate.parameters != null)
            {
                if (missionTemplate.parameters.ContainsKey("IsTargetDead"))
                {
                    if ((bool)missionTemplate.parameters["IsTargetDead"])
                    {
                        NPC = myMod.Npcs[new FormKey(myMod.ModKey, NPCTools.GetTemplateDeadNPC(isfemale))].DeepCopy();
                    }
                }
            }
            Npc npc = NPCTools.CloneNPC(myMod, NPC);
            string Gender = "Male";
            if (isfemale) Gender = "Female";

            npc.Name = AITools.RunPrompt(
                "Generate a unique full name (first and last) for a " + Gender +
                "The name should feel appropriate for someone living and operating within a criminal gang culture—gritty, believable, and grounded.\r\n" +
                "Do NOT reuse or repeat any names that have appeared previously in this session.\r\n" +
                "Do NOT include titles, ranks, nicknames, or extra commentary.\r\n" +
                "Return only the name."
            );
            npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");
            Random wrand = RandomUtils.random;
            npc.Weight = new NpcWeight()
            {
                Fat = (float)wrand.NextDouble(),
                Muscular = (float)wrand.NextDouble(),
                Thin = (float)wrand.NextDouble()
            };
            var lev = new PcLevelMult();
            lev.LevelMult = 0.25f + (float)RandomUtils.random.NextDouble();
            npc.Level = lev;
            npc.SpaceOutfit = outfit;
            npc.EyeColor = NPCTools.GetEyeColour();
            npc.HairColor = NPCTools.GetHairColour();
            npc.SkinToneIndex = (byte)wrand.Next(8);
            npc.HeadParts.Add(NPCTools.GetHaircut(isfemale));
            npc.Items = new ExtendedList<ContainerEntry>
            {
                new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } }
            };
            myMod.Npcs.Add(npc);
            newQuest.SetQuestReferenceCreateAlias("BountyTarget", npc.ToLink<IStarfieldMajorRecordGetter>());
            newQuest.SetObjective(0, "Read the " + datasource + " from " + npc.Name);

            //Book

            var booklogmessage = PromptManager.GetFirstPersonAccount(new List<string>(missionTemplate.Addons)
            {
                "Location this log leads the player to:" + nextQuest.QuestLocation + "\r\n",
                "Log Entry should mention how this character has located the next clue on the target.\r\n",
                "Current Location:" + missionTemplate.Location + "\r\n",
            });
            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            var bountybook = new BookNoun(0x0008C1, datasource, "Data Slate #" + questID, booklogmessage);
            bountybook.SetScriptProperty("duout_queststartandend", "questtoend", newQuest.instance.ToLink<IStarfieldMajorRecordGetter>());
            bountybook.SetScriptProperty("duout_queststartandend", "QuestToStart", nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>());

            var frmlst = new FormList(myMod)
            {
                EditorID = questID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            frmlst.Items.Add(bountybook.instance);
            myMod.FormLists.Add(frmlst);
            newQuest.SetScriptProperty("duout_ground_bounty_quest", "DeathItems", frmlst.ToLink<IStarfieldMajorRecordGetter>());

            
            //Set the interfaces
            questform = newQuest.instance;
            logMessage = logmessage;
            questloc = missionTemplate.Location;

            return newQuest.instance;
        }

    }
}
