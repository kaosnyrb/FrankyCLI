using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class BookNoun
    {
        public Book instance;
        public BookNoun(uint Formid, string Name, string Header, string Content) {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            IBookGetter? bookSource = targetMod.Books.FirstOrDefault(r => r.FormKey == new FormKey(targetMod.ModKey, Formid));
            if (bookSource == null)
            {
                foreach (var tm in RetrogradeContext.Current.TemplateMods)
                {
                    bookSource = tm.Books.FirstOrDefault(r => r.FormKey == new FormKey(tm.ModKey, Formid));
                    if (bookSource != null) break;
                }
            }
            if (bookSource == null)
                throw new KeyNotFoundException($"BookNoun: no Book with raw ID 0x{Formid:X6} found in target mod or any template mod.");
            var Book = bookSource.DeepCopy();
            instance = new Book(targetMod)
            {
                Components = Book.Components,
                Description = Content,
                DropdownSound = Book.DropdownSound,
                EditorID = "book_" + questID,
                Keywords = Book.Keywords,
                FeaturedItemMessage = Book.FeaturedItemMessage,
                Flags = Book.Flags,
                InventoryArt = Book.InventoryArt,
                Model = Book.Model,
                Name = Name,
                Value = Book.Value,
                Weight = Book.Weight,
                VirtualMachineAdapter = Book.VirtualMachineAdapter,
                Transforms = Book.Transforms,
            };
            targetMod.Books.Add(instance);
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
