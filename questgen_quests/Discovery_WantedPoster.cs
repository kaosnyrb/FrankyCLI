using FrankyCLI.questgen_tools;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_quests
{
    internal class Discovery_WantedPoster : IOutlawQuest
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
            Console.WriteLine("Discovery Quest - Wanted Poster.");
            questloc = missionTemplate.Location;

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            //Due  to limited wall  space,  just  make the activator

            var postername = "Wanted: " + outlawNpc.name;

            //Create the activation message
            var pickuppromt =
            "Include newline characters in your response.\r\n" +
            "Generate a short flavour text story which explains to the player that they have read a bounty poster and know the next lead to look into.\r\n\r\n" +
            "The location must match the one that is provided below.\r\n\r\n" +
            "Keep it to one paragraph with newlines and under 50 words.\r\n\r\n" +
            "Use the following information to build the explaination:\r\n\r\n";
            pickuppromt += "Location:" + nextQuest.QuestLocation + "\r\n";

            var pickupmessage = AITools.RunPrompt(pickuppromt);
            Console.WriteLine("pickupmessage: " + pickupmessage);

            var messageClone = myMod.Messages[new FormKey(myMod.ModKey, 0x000844)].DeepCopy();
            Message message = new Message(myMod)
            {
                Name = messageClone.Name,
                BNAM = messageClone.BNAM,
                EditorID = "message_" + questID,
                Description = pickupmessage,
                Flags = messageClone.Flags
            };

            myMod.Messages.Add(message);


            //Create the Activator

            var ActivatorClone = myMod.Activators[new FormKey(myMod.ModKey, 0x000836)].DeepCopy();
            Mutagen.Bethesda.Starfield.Activator newActivator = new Mutagen.Bethesda.Starfield.Activator(myMod)
            {
                ActivateSound = ActivatorClone.ActivateSound,
                Properties = ActivatorClone.Properties,
                VirtualMachineAdapter = ActivatorClone.VirtualMachineAdapter,
                ActivateTextOverride = ActivatorClone.ActivateTextOverride,
                ActivationAngle = ActivatorClone.ActivationAngle,
                Components = ActivatorClone.Components,
                Flags = ActivatorClone.Flags,
                Conditions = ActivatorClone.Conditions,
                EditorID = "wantedposter_" + questID,
                Destructible = ActivatorClone.Destructible,
                Keywords = ActivatorClone.Keywords,
                Name = postername,
                ObjectBounds = ActivatorClone.ObjectBounds,
                ODTY = ActivatorClone.ODTY,
                Model = ActivatorClone.Model,
                XALG = ActivatorClone.XALG
            };
            newActivator.Model.File = "duout\\wantedcomputer.nif";

            //Set the Current quest and next quest so when you use the activator it progresses the mission

            var activatorproperties = newActivator.VirtualMachineAdapter.Scripts[0].Properties;
            for (int i = 0; i < activatorproperties.Count; i++)
            {
                if (activatorproperties[i].Name == "messagetext")
                {
                    ((ScriptObjectProperty)newActivator.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = message.ToLink<IStarfieldMajorRecordGetter>();
                }
                //if (activatorproperties[i].Name == "currentquest")
                //{
                    //((ScriptObjectProperty)newActivator.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = newQuest.ToLink<IStarfieldMajorRecordGetter>();
                //}
                if (activatorproperties[i].Name == "nextquest")
                {
                    ((ScriptObjectProperty)newActivator.VirtualMachineAdapter.Scripts[0].Properties[i]).Object = nextQuest.questform.ToLink<IStarfieldMajorRecordGetter>();
                }
            }

            myMod.Activators.Add(newActivator);

            return null;
        }
    }
}
