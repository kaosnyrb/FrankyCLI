using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog.StructuredStrings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools.Nouns
{
    public class QuestInstance
    {
        public Quest quest;
        public QuestInstance(uint Basequest, string Questname) {
            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            var Quest = gen_quest_main.myMod.Quests[new FormKey(gen_quest_main.myMod.ModKey, Basequest)].DeepCopy();
            quest = new Quest(gen_quest_main.myMod)
            {
                Name = Questname,
                Aliases = Quest.Aliases,
                Components = Quest.Components,
                Data = Quest.Data,
                EditorID = "quest_" + questID,
                Keywords = Quest.Keywords,
                Location = Quest.Location,
                MajorFlags = Quest.MajorFlags,
                Objectives = Quest.Objectives,
                MissionTypeKeyword = Quest.MissionTypeKeyword,
                QuestType = Quest.QuestType,
                ScriptComment = Quest.ScriptComment,
                Stages = Quest.Stages,
                Summary = Quest.Summary,
                VirtualMachineAdapter = Quest.VirtualMachineAdapter
            };
            gen_quest_main.myMod.Quests.Add(quest);
        }

        public bool SetLogMessage(int StageIndex,int LogEntry, string Content)
        {
            quest.Stages[StageIndex].LogEntries[LogEntry].Entry = Content;
            return true;
        }

        public bool SetObjective(int ObjectiveIndex, string text)
        {
            quest.Objectives[ObjectiveIndex].DisplayText = text; 
            return true;
        }

        public bool SetScriptProperty(String Scriptname, String Name, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            foreach(var script in quest.VirtualMachineAdapter.Scripts)
            {
                if (script.Name == Scriptname)
                {
                    var properties = script.Properties;
                    for (int i = 0; i < properties.Count; i++)
                    {
                        if (properties[i].Name == Name)
                        {
                            ((ScriptObjectProperty)properties[i]).Object = Value;
                            return true;
                        }
                    }
                }
            }
            return false;            
        }

        public bool SetScriptAlias(int AliasIndex, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            quest.VirtualMachineAdapter.Aliases[AliasIndex].Property.Object = Value;
            return true;
        }

        public bool SetQuestReferenceCreateAlias(String AliasName, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            foreach (var alias in quest.Aliases)
            {
                try
                {
                    var CastedAlias = (IQuestReferenceAlias)alias;
                    if (CastedAlias.Name == AliasName)
                    {
                        CastedAlias.CreateReferenceToObject.Object = Value;
                        return true;
                    }
                }
                catch (Exception ex)
                {

                }

            }
            return false;
        }

        public bool SetQuestReferenceSpaceLocationAlias(String AliasName, Condition Value)
        {
            foreach (var alias in quest.Aliases)
            {
                try
                {
                    var CastedAlias = (IQuestReferenceAlias)alias;
                    if (CastedAlias.Name == AliasName)
                    {
                        CastedAlias.Conditions[0] = Value;
                        return true;
                    }
                }
                catch (Exception ex)
                {

                }

            }
            return false;
        }

        public bool SetQuestReferenceAlias(String AliasName, FormKey formKey)
        {
            foreach(var alias in quest.Aliases)
            {
                try
                {
                    var CastedAlias = (IQuestReferenceAlias)alias;
                    if ( CastedAlias.Name == AliasName)
                    {
                        CastedAlias.ForcedReference.FormKey = formKey;
                        return true;
                    }
                }
                catch (Exception ex) {

                }
                
            }
            return false;
        }

        public bool SetQuestPCMTypeKeyword(string AliasName, IFormLinkNullable<IKeywordGetter> Value)
        {
            foreach (var alias in quest.Aliases)
            {
                try
                {
                    var CastedAlias = (QuestLocationAlias)alias;
                    if (CastedAlias.Name == AliasName)
                    {
                        CastedAlias.ALPS.PcmTypeKeyword = Value;
                        return true;
                    }
                }
                catch (Exception ex)
                {

                }

            }
            return false;
        }

        public bool SetQuestLocationAlias(string AliasName, IFormLinkNullable<ILocationGetter> Value)
        {
            foreach (var alias in quest.Aliases)
            {
                try
                {
                    var CastedAlias = (IQuestLocationAlias)alias;
                    if (CastedAlias.Name == AliasName)
                    {
                        CastedAlias.SpecificLocation = Value;
                        return true;
                    }
                }
                catch (Exception ex)
                {

                }

            }
            return false;
        }

    }
}
