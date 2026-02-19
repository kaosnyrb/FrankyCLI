using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Utils
{
    public class FormKeyLookup
    {
        public static FormKey GetFormKey(string EditorID)
        {
            var targetMod = RetrogradeContext.Current.TargetMod;
            var starfieldMod = RetrogradeContext.Current.StarfieldMod;
            var templateMods = RetrogradeContext.Current.TemplateMods;

            foreach (var rec in targetMod.EnumerateMajorRecords())
            {
                if (rec.EditorID != null)
                {
                    if (rec.EditorID == EditorID)
                    {
                        return rec.FormKey;
                    }
                }
            }

            foreach (var templateMod in templateMods)
            {
                foreach (var rec in templateMod.EnumerateMajorRecords())
                {
                    if (rec.EditorID != null)
                    {
                        if (rec.EditorID == EditorID)
                        {
                            return rec.FormKey;
                        }
                    }
                }
            }

            foreach (var rec in starfieldMod.GenericBaseForms)
            {
                if (rec.EditorID != null)
                {
                    if (rec.EditorID == EditorID)
                    {
                        return rec.FormKey;
                    }
                }
            }

            foreach (var rec in starfieldMod.EnumerateMajorRecords())
            {
                if (rec.EditorID != null)
                {
                    if (rec.EditorID == EditorID)
                    {
                        return rec.FormKey;
                    }
                }
            }

            throw new KeyNotFoundException($"FormKeyLookup: no record found with EditorID '{EditorID}'. Check that the record exists in the target mod, a template mod, or Starfield.esm.");
        }
    }
}
