using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class MessageNoun
    {
        public Message instance;
        public MessageNoun(uint Formid, string Message) {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var source = RecordLookup.Find<IMessageGetter>(Formid, m => m.Messages);

            var messageClone = source.DeepCopy();
            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            instance = new Message(targetMod)
            {
                Name = messageClone.Name,
                BNAM = messageClone.BNAM,
                EditorID = "message_" + questID,
                Description = Message,
                Flags = messageClone.Flags,
                MenuButtons = messageClone.MenuButtons,
            };
            targetMod.Messages.Add(instance);
        }

        public bool SetChoice(int ChoiceID, string ChoiceText)
        {
            instance.MenuButtons[ChoiceID].Text = ChoiceText;
            return true;
        }

    }
}
