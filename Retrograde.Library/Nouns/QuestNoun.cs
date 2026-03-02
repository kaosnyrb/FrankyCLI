using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Retrograde.Nouns
{
    public class QuestNoun
    {
        public Quest instance;
        public QuestNoun(uint Basequest, string Questname, string? editorId = null) {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            IQuestGetter? source = null;
            var targetKey = new FormKey(targetMod.ModKey, Basequest);
            source = targetMod.Quests.FirstOrDefault(r => r.FormKey == targetKey);
            if (source == null)
            {
                foreach (var tm in RetrogradeContext.Current.TemplateMods)
                {
                    var tmKey = new FormKey(tm.ModKey, Basequest);
                    source = tm.Quests.FirstOrDefault(r => r.FormKey == tmKey);
                    if (source != null) break;
                }
            }
            if (source == null)
                throw new KeyNotFoundException($"QuestNoun: no Quest with raw ID 0x{Basequest:X6} found in target mod or any template mod.");

            var Quest = source.DeepCopy();
            instance = new Quest(targetMod)
            {
                Name = Questname,
                Aliases = Quest.Aliases,
                Components = Quest.Components,
                Data = Quest.Data,
                EditorID = editorId ?? "quest_" + questID,
                Keywords = Quest.Keywords,
                Location = Quest.Location,
                MajorFlags = Quest.MajorFlags,
                Objectives = Quest.Objectives,
                MissionTypeKeyword = Quest.MissionTypeKeyword,
                QuestType = Quest.QuestType,
                ScriptComment = Quest.ScriptComment,
                Stages = Quest.Stages,
                Summary = Quest.Summary,
                VirtualMachineAdapter = Quest.VirtualMachineAdapter,
                MissionBoardDescription = Quest.MissionBoardDescription,
                MissionBoardInfoPanels = Quest.MissionBoardInfoPanels,
                TextDisplayGlobals = Quest.TextDisplayGlobals,
                Event = Quest.Event                
            };
            targetMod.Quests.Add(instance);
            EnsureGangMembersFormList();
            EnsureTextDisplayGlobals();
            EnsureAliasConditionGlobals();
        }

        private void EnsureGangMembersFormList()
        {
            var targetMod = RetrogradeContext.Current.TargetMod;
            if (instance.VirtualMachineAdapter == null) return;

            foreach (var script in instance.VirtualMachineAdapter.Scripts)
            {
                for (int i = 0; i < script.Properties.Count; i++)
                {
                    if (script.Properties[i].Name != "GangMembers") continue;
                    if (script.Properties[i] is not ScriptObjectProperty objProp) continue;

                    var fk = objProp.Object.FormKey;
                    if (fk.IsNull) return;

                    // Find the source FormList to get its EditorID
                    IFormListGetter? source = null;
                    source = targetMod.FormLists.FirstOrDefault(r => r.FormKey == fk);
                    if (source == null)
                    {
                        foreach (var tm in RetrogradeContext.Current.TemplateMods)
                        {
                            source = tm.FormLists.FirstOrDefault(r => r.FormKey == fk);
                            if (source != null) break;
                        }
                    }
                    if (source == null) return;

                    // If targetMod already has a FormList with the same EditorID, point to it
                    var existing = targetMod.FormLists.FirstOrDefault(r => r.EditorID == source.EditorID);
                    if (existing != null)
                    {
                        objProp.Object = existing.ToLink<IStarfieldMajorRecordGetter>();
                        return;
                    }

                    // Otherwise create a new FormList in targetMod (fresh FormKey, same contents)
                    var newList = new FormList(targetMod)
                    {
                        EditorID = source.EditorID,
                    };
                    if (source.Items != null)
                        foreach (var item in source.Items)
                            newList.Items.Add(item.FormKey.ToLink<IStarfieldMajorRecordGetter>());
                    targetMod.FormLists.Add(newList);
                    objProp.Object = newList.ToLink<IStarfieldMajorRecordGetter>();
                    return;
                }
            }
        }

        private void EnsureTextDisplayGlobals()
        {
            var targetMod = RetrogradeContext.Current.TargetMod;
            if (instance.TextDisplayGlobals == null || instance.TextDisplayGlobals.Count == 0) return;

            var templateMods = RetrogradeContext.Current.TemplateMods;

            for (int i = 0; i < instance.TextDisplayGlobals.Count; i++)
            {
                var fk = instance.TextDisplayGlobals[i].FormKey;
                if (fk.IsNull) continue;

                bool isTemplateMod = templateMods.Any(tm => tm.ModKey == fk.ModKey);
                if (!isTemplateMod) continue;

                var local = GetOrCreateLocalGlobal(fk, templateMods, targetMod);
                if (local != null)
                    instance.TextDisplayGlobals[i] = local.ToLink<IGlobalGetter>();
            }
        }

        private void EnsureAliasConditionGlobals()
        {
            if (instance.Aliases == null) return;

            var targetMod = RetrogradeContext.Current.TargetMod;
            var templateMods = RetrogradeContext.Current.TemplateMods;

            foreach (var alias in instance.Aliases)
            {
                // Reference aliases
                if (alias is IQuestReferenceAlias refAlias && refAlias.Conditions != null)
                    FixConditionGlobals(refAlias.Conditions, templateMods, targetMod);

                // Location aliases (hold the distance global in their conditions)
                if (alias is QuestLocationAlias locAlias && locAlias.Conditions != null)
                    FixConditionGlobals(locAlias.Conditions, templateMods, targetMod);
            }
        }

        private void FixConditionGlobals(IList<Condition> conditions,
            IReadOnlyList<IStarfieldModGetter> templateMods, StarfieldMod targetMod)
        {
            for (int i = 0; i < conditions.Count; i++)
            {
                // ConditionFloat: global referenced as FirstParameter of a function (e.g. GetGlobalValue)
                if (conditions[i] is ConditionFloat cf && cf.Data is GetGlobalValueConditionData gvData)
                {
                    var fk = gvData.FirstParameter.Link.FormKey;
                    if (!fk.IsNull && templateMods.Any(tm => tm.ModKey == fk.ModKey))
                    {
                        var local = GetOrCreateLocalGlobal(fk, templateMods, targetMod);
                        if (local != null)
                            gvData.FirstParameter = new FormLinkOrIndex<IGlobalGetter>(gvData, local.FormKey);
                    }
                }

                // ConditionGlobal: global is the comparison value (e.g. GetDistanceGalacticParsec < distanceGlobal)
                if (conditions[i] is ConditionGlobal cg)
                {
                    var fk = cg.ComparisonValue.FormKey;
                    if (!fk.IsNull && templateMods.Any(tm => tm.ModKey == fk.ModKey))
                    {
                        var local = GetOrCreateLocalGlobal(fk, templateMods, targetMod);
                        if (local != null)
                            cg.ComparisonValue.SetTo(local.FormKey);
                    }
                }
            }
        }

        private Global? GetOrCreateLocalGlobal(FormKey templateGlobalKey,
            IReadOnlyList<IStarfieldModGetter> templateMods, StarfieldMod targetMod)
        {
            IGlobalGetter? source = null;
            foreach (var tm in templateMods)
            {
                source = tm.Globals.FirstOrDefault(r => r.FormKey == templateGlobalKey);
                if (source != null) break;
            }
            if (source == null) return null;

            var existing = targetMod.Globals.FirstOrDefault(r => r.EditorID == source.EditorID);
            if (existing is Global g) return g;

            var newGlobal = new Global(targetMod)
            {
                EditorID = source.EditorID,
                Data = (source as Global)?.Data ?? 0f
            };
            targetMod.Globals.Add(newGlobal);
            Console.WriteLine($"  [QuestNoun] Copied template global '{newGlobal.EditorID}' into target mod.");
            return newGlobal;
        }

        public bool SetLogMessage(int StageIndex,int LogEntry, string Content)
        {
            instance.Stages[StageIndex].LogEntries[LogEntry].Entry = Content;
            return true;
        }

        public bool SetObjective(int ObjectiveIndex, string text)
        {
            instance.Objectives[ObjectiveIndex].DisplayText = text;
            return true;
        }

        public bool SetScriptProperty(String Scriptname, String Name, IFormLink<IStarfieldMajorRecordGetter>? Value)
        {
            if (Value == null)
                throw new ArgumentNullException(nameof(Value), $"SetScriptProperty: null link passed for script '{Scriptname}' property '{Name}'. The record was not found in any loaded mod.");
            foreach(var script in instance.VirtualMachineAdapter.Scripts)
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

        public bool SetScriptPropertyStruct(String Scriptname, String Name,String Structname, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            foreach (var script in instance.VirtualMachineAdapter.Scripts)
            {
                if (script.Name == Scriptname)
                {
                    var properties = script.Properties;
                    for (int i = 0; i < properties.Count; i++)
                    {
                        if (properties[i].Name == Name)
                        {
                            var structs = ((ScriptStructListProperty)properties[i]).Structs;
                            for (int j = 0; j < structs.Count;j++)
                            {
                                for(int k = 0;k< structs[j].Members.Count;k++)
                                {
                                    if (structs[j].Members[k].Name  == Structname)
                                    {
                                        ((ScriptObjectProperty)structs[j].Members[k]).Object = Value;
                                        return true;
                                    }
                                }
                            }
                            
                        }
                    }
                }
            }
            return false;
        }


        public bool SetScriptProperty(String Scriptname, String Name, int Value)
        {
            foreach (var script in instance.VirtualMachineAdapter.Scripts)
            {
                if (script.Name == Scriptname)
                {
                    var properties = script.Properties;
                    for (int i = 0; i < properties.Count; i++)
                    {
                        if (properties[i].Name == Name)
                        {
                            ((ScriptIntProperty)properties[i]).Data = Value;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool SetScriptAlias(int AliasIndex, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            instance.VirtualMachineAdapter.Aliases[AliasIndex].Property.Object = Value;
            return true;
        }

        public bool SetScriptAliasScriptObject(string scriptname, string scriptprop, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            foreach (var Alias  in instance.VirtualMachineAdapter.Aliases)
            {
                foreach (var script in Alias.Scripts)
                {
                    if (script.Name == scriptname)
                    {
                        var properties = script.Properties;
                        for (int i = 0; i < properties.Count; i++)
                        {
                            if (properties[i].Name == scriptprop)
                            {
                                ((ScriptObjectProperty)properties[i]).Object = Value;
                                return true;
                            }
                        }
                    }
                }
            }
            return true;
        }

        public bool SetScriptFragmentAlias(string AliasName, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            var fragments = instance.VirtualMachineAdapter.Script;
            var properties = fragments.Properties;
            for (int i = 0; i < properties.Count; i++)
            {
                if (properties[i].Name == AliasName)
                {
                    ((ScriptObjectProperty)properties[i]).Object = Value;
                    return true;
                }
            }
            return true;
        }

        public bool SetQuestReferenceCreateAlias(String AliasName, IFormLink<IStarfieldMajorRecordGetter> Value)
        {
            foreach (var alias in instance.Aliases)
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
            foreach (var alias in instance.Aliases)
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
            foreach(var alias in instance.Aliases)
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
            foreach (var alias in instance.Aliases)
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
            foreach (var alias in instance.Aliases)
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

        public bool SetQuestLevelledSpaceCellAlias(int AliasId, IFormLinkNullable<ILeveledSpaceCellGetter> Value)
        {
            var collection = (QuestCollectionAlias)instance.Aliases[AliasId];
            collection.Collection[0].ReferenceAlias.CreateReferenceToObject.Object = Value;
            return true;
        }


    }
}
