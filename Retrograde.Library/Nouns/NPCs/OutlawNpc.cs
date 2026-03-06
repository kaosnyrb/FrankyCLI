using Retrograde.AI;
using Retrograde.AI.Utils;
using Retrograde.Utils;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class OutlawNpc : INoun<INpcGetter>
    {
        public StarfieldMod myMod;

        public string name;
        public string gender;

        public string Haircolor;
        public string Eyecolor;

        public string BountyFaction;

        public bool spacesuit;

        public bool female;

        public Npc instance;
        public INpcGetter Result => instance;
        string? INoun.EditorID => instance?.EditorID;
        FormKey INoun.FormKey => instance?.FormKey ?? FormKey.Null;

        public FormKey deathItems;

        public FormKey Logfile;
        public string LogText = string.Empty;

        public OutlawTraits Traits = new OutlawTraits();




        public OutlawNpc(StarfieldMod myModparam, bool hasspacesuit) {

            if (RandomProvider.Random.Next(100) > 50)
            {
                female = true;
            }
            myMod = myModparam;

            if (female)
            {
                gender = "female";
            }
            else
            {
                gender = "male";
            }

            spacesuit = hasspacesuit;

            var voicePool = female ? VoiceSeedData.FemaleVoices : VoiceSeedData.MaleVoices;
            var voice = voicePool[RandomProvider.Random.Next(voicePool.Count)];
            ElevenLabsVoiceId   = voice.Id;
            ElevenLabsVoiceName = voice.Name;

            Haircolor = NPCTools.GetHairColour();
            Eyecolor = NPCTools.GetEyeColour();

            Console.WriteLine("Building Outlaw NPC...");
            name = NameSeedData.GenerateName(female);

            Traits = OutlawTraits.Generate();
        }

        public string VoiceEditorId = string.Empty;
        public string ElevenLabsVoiceId = string.Empty;
        public string ElevenLabsVoiceName = string.Empty;

        public Npc GenerateNPC()
        {
            var NPC = NPCTools.FindTemplateNpc(female);
            Npc npc = NPCTools.CloneNPC(myMod, NPC);
            npc.Name = name;
            npc.EditorID = "npc_" + (name.ToLower()).Replace(" ","");

            // Set voice type after construction (Mutagen nullable FormLink rule).
            // BountyFaction may not be set in all chains; GetVoice defaults to GenericMale/Female.
            var voice = NPCTools.GetVoice(BountyFaction ?? "", female);
            if (!voice.IsNull)
            {
                npc.Voice.SetTo(voice.FormKey);
                var sfMod = RetrogradeContext.Current.StarfieldMod;
                var vtRec = sfMod.VoiceTypes.FirstOrDefault(v => v.FormKey == voice.FormKey);
                VoiceEditorId = vtRec?.EditorID ?? voice.FormKey.ID.ToString("X6");
            }

            Random wrand = RandomProvider.Random;
            foreach (var facemorph in npc.FaceMorphs)
            {
                foreach (var inner in facemorph.MorphGroups)
                {
                    inner.BlendIntensity = (float)wrand.NextDouble();
                }
            }
            npc.Weight = new NpcWeight()
            {
                Fat = (float)wrand.NextDouble(),
                Muscular = (float)wrand.NextDouble(),
                Thin = (float)wrand.NextDouble()
            };

            npc.SpaceOutfit = NPCTools.GetRandomOutfit(spacesuit);
            npc.EyeColor = Eyecolor;
            npc.HairColor = Haircolor;
            npc.SkinToneIndex = (byte)wrand.Next(8);
            npc.HeadParts.Add(NPCTools.GetHaircut(female));
            var lev = new PcLevelMult();
            lev.LevelMult = 0.25f + (float)wrand.NextDouble();
            npc.Level = lev;

            npc.Items = new ExtendedList<ContainerEntry>
            {
                new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } },
            };

            var frmlst = new FormList(myMod)
            {
                EditorID = npc.EditorID + "_deathitems",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };

            deathItems = frmlst.FormKey;

            myMod.FormLists.Add(frmlst);

            myMod.Npcs.Add(npc);
            instance = npc;
            RetrogradeContext.NounRegistry.Add(this);
            return npc;
        }

        // Called after all stage narratives are generated so the AI can draw on the full quest story.
        public void GenerateLegendaryItem()
        {
            Console.WriteLine("Generating Legendary Item...");
            var legendary = new LegendaryArmourNoun(name);
            myMod.FormLists[deathItems].Items.Add(legendary.LeveledItemGetter);
        }

        //We do this last as we've built all the infomation to use in it.
        public void GenerateLog()
        {
            var log = NarrativePrompts.GetOutlawLogfile(name, gender, Traits);
            LogText = log;
            var bookSrc = RecordLookup.Find<IBookGetter>(0x000800u, m => m.Books);
            var Book = bookSrc.DeepCopy();
            string logSynonym = RandomProvider.GetLogSynonym();
            Book logbook = new Book(myMod)
            {
                Components = Book.Components,
                Text = log,
                DropdownSound = Book.DropdownSound,
                EditorID = "book_" + (name.ToLower()).Replace(" ", ""),
                Keywords = Book.Keywords,
                FeaturedItemMessage = Book.FeaturedItemMessage,
                Flags = Book.Flags,
                InventoryArt = Book.InventoryArt,
                Model = Book.Model,
                Name = name + " " + logSynonym,
                Value = Book.Value,
                Weight = Book.Weight,
                Transforms = Book.Transforms,
            };

            myMod.Books.Add(logbook);

            //Add logbook to death items
            Logfile = logbook.FormKey;

            myMod.FormLists[deathItems].Items.Add(logbook);
        }



        public static string GetNationality()
        {
            var r = RandomProvider.Random;
            return NpcSeedData.Nationalities[r.Next(NpcSeedData.Nationalities.Count)];
        }

    }
}
