using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Utils
{
    public class FormKeyLookup
    {
        public static FormKey GetFormKey(string EditorID)
        {
            foreach (var rec in gen_quest_main.myMod.EnumerateMajorRecords())
            {
                if (rec.EditorID != null)
                {
                    if (rec.EditorID == EditorID)
                    {
                        return rec.FormKey;
                    }
                }
            }
            
            foreach(var rec in gen_quest_main._StarfieldMod.EnumerateMajorRecords())
            {
                if (rec.EditorID != null)
                {
                    if (rec.EditorID == EditorID)
                    {
                        return rec.FormKey;
                    }
                }
            }

            return new FormKey();
        }
    }
}
