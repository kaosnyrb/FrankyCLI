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
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.Nouns
{
    public class OutlawNpc
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

        public FormKey deathItems;

        public FormKey Logfile;
        public string LogText = string.Empty;




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

            var voicePool = female ? SeedManager.FemaleVoices : SeedManager.MaleVoices;
            var voice = voicePool[RandomProvider.Random.Next(voicePool.Count)];
            ElevenLabsVoiceId   = voice.Id;
            ElevenLabsVoiceName = voice.Name;

            Haircolor = NPCTools.GetHairColour();
            Eyecolor = NPCTools.GetEyeColour();

            Console.WriteLine("Building Outlaw NPC...");
            name = SeedManager.GenerateName(female);
        }

        public string GenerateLogfile()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Write a personal audio log recorded by " + name + ", a " + gender + " fugitive who has a bounty on their head.");
            sb.AppendLine("This is a spoken monologue recorded alone into a data-slate — write it for voice performance, not for reading.");
            sb.AppendLine();

            // Use the quest's LoreContext if available (it's always set before GenerateLog is called in the chain).
            // Fall back to random seeds only if running standalone.
            if (!string.IsNullOrEmpty(PromptManager.LoreContext))
            {
                sb.AppendLine("Character context — use the LoreContext established earlier in this conversation. It is the source of truth for who this person is and why they're running.");
            }
            else
            {
                string upbringing = GetUpbringing();
                string fear       = GetFears();
                string goal       = GetGoals();
                string flaw       = Getflaws();

                sb.AppendLine("Character context:");
                sb.AppendLine("- Background: " + upbringing);
                sb.AppendLine("- Core fear: " + fear);
                sb.AppendLine("- Motivation: " + goal);
                sb.AppendLine("- Personality flaw: " + flaw);
                if (!string.IsNullOrEmpty(BountyFaction))
                    sb.AppendLine("- Currently hunted by: " + BountyFaction);
            }
            sb.AppendLine();
            var rng = RandomProvider.Random;
            string tone  = SeedManager.LogTones[rng.Next(SeedManager.LogTones.Count)];
            string focus = SeedManager.LogFocusPoints[rng.Next(SeedManager.LogFocusPoints.Count)];

            sb.AppendLine("Tone: " + tone + ".");
            sb.AppendLine();
            sb.AppendLine("Voice delivery rules — follow these exactly:");
            sb.AppendLine("- You may use these audio tags sparingly, only where they add genuine stress or fear: [sighs], [whispers], [exhales sharply].");
            sb.AppendLine("- Do NOT use [laughs] or any tag suggesting levity or relief.");
            sb.AppendLine("- Use ellipses (...) for hesitation, a thought that collapses, or words they can't finish.");
            sb.AppendLine("- Use an em dash (—) for an abrupt self-correction or a thought cut short by nerves.");
            sb.AppendLine("- CAPITALIZE a single word only when fear or desperation forces it out louder than the rest.");
            sb.AppendLine("- Write as natural stressed speech: stumbles, fragments, and restarts are right for this character.");
            sb.AppendLine("- No headers, bullet points, or any formatting — this is pure spoken audio.");
            sb.AppendLine("- Do NOT open with any recording preamble — no 'Recording...', no stating their name, no date stamp. Go straight into the content.");
            sb.AppendLine();
            sb.AppendLine("Content:");
            sb.AppendLine("- The emotional core of this log is: " + focus + ".");
            sb.AppendLine("- Total length: under 140 words. Every word will be read aloud, so make each one count.");

            Console.WriteLine("Generating Outlaw Log...");

            string background = AITools.RunPrompt(sb.ToString());
            return background;
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
            var log = GenerateLogfile();
            LogText = log;
            IBookGetter? bookSrc = myMod.Books.FirstOrDefault(r => r.FormKey == new FormKey(myMod.ModKey, 0x000800));
            if (bookSrc == null)
                foreach (var tm in RetrogradeContext.Current.TemplateMods)
                {
                    bookSrc = tm.Books.FirstOrDefault(r => r.FormKey == new FormKey(tm.ModKey, 0x000800));
                    if (bookSrc != null) break;
                }
            if (bookSrc == null)
                throw new KeyNotFoundException("OutlawNpc: no Book with raw ID 0x000800 found in target mod or any template mod.");
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



        public string GetUpbringing()
        {
            Random random = RandomProvider.Random;

            List<string> upbringinglist = new List<string>()
            {
                "Grew up in the city of New Atlantis, their parents worked in MAST admin.",
                "Grew up in the city of New Atlantis, their parents worked in the UC Navy.",
                "Grew up in the city of Neon, as a streetrat on the Ebbside.",
                "Grew up in the city of Akila, as an orphan on The Stretch.",
                "Grew up drifting system to system as a spacer kid aboard a family owned hauler.",
            };

            return upbringinglist[random.Next(upbringinglist.Count)];
        }

        public string Getflaws()
        {
            Random random = RandomProvider.Random;
            List<string> personalityFlaws = new List<string>()
            {
                "Impulsive",
                "Overly stubborn",
                "Easily angered",
                "Overconfident",
                "Pessimistic",
            };

            return personalityFlaws[random.Next(personalityFlaws.Count)];
        }

        public string GetTrait()
        {
            Random random = RandomProvider.Random;

            List<string> traitlist = new List<string>()
            {
                "Short temper",
                "Good hearing",
                "Night owl",
                "Tech savvy",
                "Fearless",
            };

            return traitlist[random.Next(traitlist.Count)];
        }

        public string GetHabit()
        {
            List<string> habitsAndBehaviors = new List<string>()
            {
                "Always cleans their gear",
                "Talks with their hands",
                "Constantly taps their foot",
                "Writes everything down",
                "Double-checks all locks",
            };

            Random random = RandomProvider.Random;

            return habitsAndBehaviors[random.Next(habitsAndBehaviors.Count)];
        }

        public string GetFears()
        {
            Random random = RandomProvider.Random;
            List<string> fearsAndPhobias = new List<string>()
            {
                "Fear of heights",
                "Fear of deep water",
                "Fear of small spaces",
                "Fear of the dark",
                "Fear of being alone",
            };

            return fearsAndPhobias[random.Next(fearsAndPhobias.Count)];
        }

        public string GetGoals()
        {
            Random random = RandomProvider.Random;
            List<string> motivationsAndGoals = new List<string>()
            {
                "Seeking wealth",
                "Seeking fame",
                "Seeking revenge",
                "Searching for lost family",
                "Trying to escape their past",
            };

            return motivationsAndGoals[random.Next(motivationsAndGoals.Count)];
        }

        public static string GetNationality()
        {
            Random random = RandomProvider.Random;

            List<string> nationalityList = new List<string>()
            {
                "American",
                "British",
                "Canadian",
                "Mexican",
                "Brazilian",
                "French",
                "German",
                "Japanese",
                "Chinese",
                "Russian",
            };

            return nationalityList[random.Next(nationalityList.Count)];
        }

    }
}
