using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class ActivatorNoun
    {
        public Mutagen.Bethesda.Starfield.Activator instance;
        public ActivatorNoun(uint FormID, string Name, string model) {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var questID = Guid.NewGuid().ToString().Substring(0, 8);

            var ActivatorClone = targetMod.Activators[new FormKey(targetMod.ModKey, FormID)].DeepCopy();
            instance = new Mutagen.Bethesda.Starfield.Activator(targetMod)
            {
                ActivateSound = ActivatorClone.ActivateSound,
                Properties = ActivatorClone.Properties,
                VirtualMachineAdapter = ActivatorClone.VirtualMachineAdapter,
                ActivateTextOverride = ActivatorClone.ActivateTextOverride,
                ActivationAngle = ActivatorClone.ActivationAngle,
                Components = ActivatorClone.Components,
                Flags = ActivatorClone.Flags,
                Conditions = ActivatorClone.Conditions,
                EditorID = "activator_" + questID,
                Destructible = ActivatorClone.Destructible,
                Keywords = ActivatorClone.Keywords,
                Name = Name,
                ObjectBounds = ActivatorClone.ObjectBounds,
                Model = ActivatorClone.Model,
                XALG = ActivatorClone.XALG
            };
            instance.Model.File = model;

            targetMod.Activators.Add(instance);
        }

        public bool SetScriptProperty(String Scriptname, String Name, IFormLink<IStarfieldMajorRecordGetter> Value)
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
                            ((ScriptObjectProperty)properties[i]).Object = Value;
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
}
