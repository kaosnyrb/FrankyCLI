using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
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

            IMessageGetter? source = null;
            var targetKey = new FormKey(targetMod.ModKey, Formid);
            source = targetMod.Messages.FirstOrDefault(r => r.FormKey == targetKey);

            if (source == null)
            {
                foreach (var tm in RetrogradeContext.Current.TemplateMods)
                {
                    var tmKey = new FormKey(tm.ModKey, Formid);
                    var tmMsg = tm.Messages.FirstOrDefault(r => r.FormKey == tmKey);
                    if (tmMsg != null)
                    {
                        source = tmMsg;
                        break;
                    }
                }
            }

            if (source == null)
                throw new KeyNotFoundException($"MessageNoun: no Message with raw ID 0x{Formid:X6} found in target mod or any template mod.");

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
