using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class BookNoun : INoun<IBookGetter>
    {
        public Book instance;
        public IBookGetter Result => instance;
        string? INoun.EditorID => instance.EditorID;
        FormKey INoun.FormKey => instance.FormKey;
        public BookNoun(string editorId, string Name, string Content) {
            var targetMod = RetrogradeContext.Current.TargetMod;

            var questID = Guid.NewGuid().ToString().Substring(0, 8);
            var bookSource = RecordLookup.Find<IBookGetter>(editorId, m => m.Books);
            var Book = bookSource.DeepCopy();
            instance = new Book(targetMod)
            {
                Components = Book.Components,
                Text = Content,
                Description = "",
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
                DataSlateType = Book.DataSlateType                
            };
            targetMod.Books.Add(instance);
            RetrogradeContext.NounRegistry.Add(this);
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
