using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.questgen_tools.Nouns
{
    public class MessageBox
    {
        public Message message;
        public MessageBox(uint Formid, string Message) {
            
            var messageClone = gen_quest_main.myMod.Messages[new FormKey(gen_quest_main.myMod.ModKey, Formid)].DeepCopy();
            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            message = new Message(gen_quest_main.myMod)
            {
                Name = messageClone.Name,
                BNAM = messageClone.BNAM,
                EditorID = "message_" + questID,
                Description = Message,
                Flags = messageClone.Flags
            };
            gen_quest_main.myMod.Messages.Add(message);
        }

    }
}
