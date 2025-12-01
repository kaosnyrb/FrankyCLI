using DynamicData;
using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using OpenAI;
using OpenAI.Chat;
using System;
using System;
using System.Collections.Generic;
using System.Collections.Generic;
using System.Linq;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools
{
    public class SpeechTools
    {
        public SpeechTools() { }

        public static void AddVoice(uint BookID, uint QuestID,string text)
        {
            uint formid = 0;
            IMajorRecord formrec = null;

            //Fetch the Mod Quest
            var currentquest = gen_quest.myMod.Quests[new FormKey(gen_quest.myMod.ModKey, QuestID)];


            //Fetch the Sample Sound Quest
            var quest = gen_quest._StarfieldMod.Quests[new FormKey(gen_quest.StarfieldModKey, 0x21A1CA)];

            var clonetopic = quest.DialogTopics[0].DeepCopy();

            var clonescene = quest.Scenes[0].DeepCopy();

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var topic = new DialogTopic(gen_quest.myMod)
            {
                EditorID = "topic_" + questID,
                Priority = clonetopic.Priority,
                SubtypeName = clonetopic.SubtypeName,
                Subtype = clonetopic.Subtype,
                TopicInfoList = new ExtendedList<IFormLinkGetter<IDialogResponsesGetter>>()
                {

                },
                
            };
            topic.TopicInfoList.Clear();


            DialogResponse dialogResponse = new DialogResponse() { };

            topic.TopicInfoList.Add((IFormLinkGetter<IDialogResponsesGetter>)dialogResponse);

            //currentquest.DialogTopics.Add(topic);
             

            var Scene = new Scene(gen_quest.myMod)
            {
                EditorID = "scene_" + questID,
                Phases = clonescene.Phases,
                Actors = clonescene.Actors,
                VNAM = clonescene.VNAM,
                BOLV = clonescene.BOLV,
                Index = clonescene.Index,
                Actions = clonescene.Actions,
            };

            ((RadioSceneAction)Scene.Actions[0]).Topic = topic.ToLink<IDialogTopicGetter>();


            currentquest.Scenes.Add(Scene);

            var book = gen_quest.myMod.Books[new FormKey(gen_quest.myMod.ModKey, BookID)];

            book.Scene = Scene.ToNullableLink<ISceneGetter>();
        }
    }
}
