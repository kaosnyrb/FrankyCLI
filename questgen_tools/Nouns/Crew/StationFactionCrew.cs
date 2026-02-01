using DynamicData;
using FrankyCLI.questgen_tools.Interfaces;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System.Data;

namespace FrankyCLI.questgen_tools
{
    public class StationFactionCrew : ICrew
    {
        public static string GetCrewJobRole(string Faction)
        {
            Random r = RandomUtils.random;

            List<string> roles = new List<string>();
            switch (Faction)
            {
                case "Crimsonfleet":
                    roles = new List<string>
                    {
                        "Boarding Boss","Boarding Mate","Boarding Hand","Salvage Boss","Salvage Rigger",
                        "Salvage Hand","Dock Boss","Dock Enforcer","Cargo Boss","Cargo Runner",
                        "Scrap Boss","Scrap Cutter","Reactor Watch","Fuel Runner","Gun Deck Boss",
                        "Gun Deck Hand","Quarter Boss","Supply Runner","Hull Boss","Hull Rigger",
                        "First Mate","Fleet Boss","Void Commodore","Harbor Master",
                        "Raid Coordinator","Lootmaster","Black Quartermaster","Dock Overseer","Salvage Overseer",
                        "Security Overseer","Gun Deck Captain","Operations Boss","Supply Boss","Fuel Boss",
                        "Hullmaster","Chief Rigger","Iron Boss","Void Boss","Blood Boss",
                        "Dock Boss","Shift Boss","Deck Boss","Crew Boss",
                        "Void Runner","Cargo Runner","Fuel Runner","Supply Runner",
                        "Hull Rigger","Cable Rigger","Patch Rigger","Void Rigger",
                        "Scrap Hand","Deck Hand","Dock Hand","Hull Hand",
                        "Systems Watch","Reactor Watch","Air Watch","Gate Watch",
                        "Gun Deck Hand","Security Hand","Lockup Guard","Gate Guard",
                        "Salvage Hand","Scrap Cutter","Plasma Cutter","Torch Hand",
                        "Quarter Hand","Stores Hand","Loot Clerk","Cargo Clerk",
                        "Void Tech","Patch Tech","Jury Tech","Rig Tech"
                    };
                    break;
                case "Ecliptic":
                    roles = new List<string>
                    {
                        "Contractor","Field Contractor","Licensed Contractor","Senior Contractor",
                        "Operative","Field Operative","Assault Operative","Tactical Operative",
                        "Mercenary","Line Mercenary","Heavy Mercenary","Veteran Mercenary",
                        "Trooper","Shock Trooper","Void Trooper","Breacher",
                        "Specialist","Weapons Specialist","Demolitions Specialist","Systems Specialist",
                        "Handler","Squad Handler","Asset Handler",
                        "Sergeant","Section Sergeant","Platoon Sergeant",
                        "Lieutenant","Field Lieutenant",
                        "Captain","Company Captain",
                        "Operations Chief","Tactical Chief","Security Chief",
                        "Field Commander","Strike Commander",
                        "Director","Regional Director","Operations Director",
                        "Rifleman","Marksman","Heavy Rifleman","Breacher",
                        "Specialist","Weapons Specialist","Demolitions Specialist","Systems Specialist",
                        "Handler","Asset Handler","Squad Handler","Team Handler",
                        "Sergeant","Section Sergeant","Field Sergeant","Shift Sergeant"
                    };

                    break;
                case "Varuun":
                    roles = new List<string>
                    {
                        "Initiate","Void Initiate","Serpent Initiate","Ash Initiate",
                        "Acolyte","Void Acolyte","Serpent Acolyte","Ash Acolyte",
                        "Devotee","Void Devotee","Serpent Devotee","Ash Devotee",
                        "Disciple","Void Disciple","Serpent Disciple","Ash Disciple",
                        "Zealot","Void Zealot","Serpent Zealot","Ash Zealot",
                        "Ascetic","Void Ascetic","Serpent Ascetic","Ash Ascetic",
                        "Preacher","Void Preacher","Serpent Preacher",
                        "Hierophant","Void Hierophant",
                        "High Zealot","Serpent Prophet","Void Prophet",
                        "Seeker","Void Seeker","Faith Seeker","Truth Seeker",
                        "Initiate","Junior Initiate","Outer Initiate","Lay Initiate",
                        "Acolyte","Lesser Acolyte","Lay Acolyte","Outer Acolyte",
                        "Devotee","Minor Devotee","Lay Devotee","Faith Devotee",
                        "Follower","Faith Follower","Outer Follower","Bound Follower",
                        "Supplicant","Humble Supplicant","Bound Supplicant","Outer Supplicant",
                        "Aspirant","Faith Aspirant","Lay Aspirant","Outer Aspirant",
                        "Attendant","Rite Attendant","Temple Attendant","Outer Attendant",
                        "Witness","Faith Witness","Outer Witness","Bound Witness"
                    };
                    break;
                case "Spacer":
                    roles = new List<string>
                    {
                        "Drifter","Void Drifter","Dock Drifter","Hull Drifter",
                        "Rigger","Hull Rigger","Cable Rigger","Void Rigger",
                        "Runner","Dock Runner","Cargo Runner","Fuel Runner",
                        "Scrapper","Hull Scrapper","Wire Scrapper","Void Scrapper",
                        "Loader","Dock Loader","Crate Loader","Bulk Loader",
                        "Hand","Deck Hand","Dock Hand","Hull Hand",
                        "Tech Hand","Patch Tech","Cable Tech","Fuel Tech",
                        "Watcher","Air Watch","Reactor Watch","Gate Watch",
                        "Scav","Dock Scav","Void Scav","Scrap Scav",
                        "Runner","Hot Runner","Cargo Runner","Fuel Runner",
                        "Rigger","Hull Rigger","Cable Rigger","Maintenance Rigger",
                        "Scrapper","Chop Scrapper","Hull Scrapper","Wire Scrapper",
                        "Hand","Dirty Hand","Dock Hand","Hull Hand",
                        "Scav","Dock Scav","Scrap Scav","Cargo Scav",
                        "Tech","Jury Tech","Patch Tech","Rig Tech",
                        "Guard","Gate Guard","Lockup Guard","Cargo Guard",
                        "Enforcer","Crew Enforcer","Dock Enforcer","Station Enforcer"
                    };

                    break;
                default:
                    roles = new List<string>
                    {
                        "Technician","Systems Technician","Maintenance Tech","Service Tech",
                        "Engineer","Systems Engineer","Station Engineer","Support Engineer",
                        "Operator","Control Operator","Reactor Operator","Dock Operator",
                        "Attendant","Service Attendant","Hab Attendant","Medical Attendant",
                        "Clerk","Logistics Clerk","Supply Clerk","Records Clerk",
                        "Coordinator","Operations Coordinator","Traffic Coordinator","Shift Coordinator",
                        "Supervisor","Floor Supervisor","Systems Supervisor","Dock Supervisor",
                        "Manager","Operations Manager","Facilities Manager","Logistics Manager",
                        "Analyst","Systems Analyst","Operations Analyst","Safety Analyst",
                        "Resident","Station Resident","Civilian","Station Civilian",
                        "Worker","Station Worker","Service Worker","Utility Worker",
                        "Technician","Maintenance Technician","Service Technician","Support Technician",
                        "Attendant","Service Attendant","Hab Attendant","Transit Attendant",
                        "Clerk","Supply Clerk","Records Clerk","Logistics Clerk",
                        "Operator","Control Operator","Systems Operator","Transit Operator",
                        "Custodian","Station Custodian","Sanitation Worker","Maintenance Custodian",
                        "Vendor","Shop Vendor","Market Vendor","Food Vendor",
                        "Assistant","Office Assistant","Service Assistant","Operations Assistant"
                    };
                    break;
            }

            return roles[r.Next(roles.Count)];
        }

        public IFormLink<IStarfieldMajorRecordGetter> GetCrewFormList(string Faction,string ShipName)
        {
            var frmlst = new FormList(gen_quest_main.myMod)
            {
                EditorID = ShipName + "_crewlist",
                Items = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>(),
            };
            //Dead Named Crew
            var list = new ExtendedList<IFormLinkGetter<IStarfieldMajorRecordGetter>>();
            Random random = RandomUtils.random;
            //Generate a new NPC
            string Crewname = Faction;

            int crewcount = 10;

            for (int i = 0; i < crewcount; i++)
            {
                bool isfemale = false;
                if (random.Next(100) > 50)
                {
                    isfemale = true;
                }
                var outfit = NPCTools.GetRandomFactionOutfit(Faction);

                var NPC = gen_quest_main.myMod.Npcs[new FormKey(gen_quest_main.myMod.ModKey, NPCTools.GetTemplateNPC(isfemale))].DeepCopy();
                Npc npc = NPCTools.CloneNPC(gen_quest_main.myMod, NPC);

                //Name
                //Console.WriteLine("Generating Crew Name...");
                string Gender = "Male";
                if (isfemale) Gender = "Female";
                if (random.Next(100) > 90)
                {
                    //Generic name
                    npc.Name = Crewname + " " + GetCrewJobRole("");
                }
                else
                {
                    npc.Name = Crewname + " " + GetCrewJobRole(Faction);
                }
                npc.EditorID = "npc_" + (npc.Name.ToString().ToLower()).Replace(" ", "");


                npc.Voice = NPCTools.GetVoice(Faction, isfemale);
                npc.Factions.Clear();
                npc.Factions.Add(NPCTools.GetFaction(Faction));

                Random wrand = RandomUtils.random;
                npc.Weight = new NpcWeight()
                {
                    Fat = (float)wrand.NextDouble(),
                    Muscular = (float)wrand.NextDouble(),
                    Thin = (float)wrand.NextDouble()
                };
                var lev = new PcLevelMult();
                lev.LevelMult = (float)random.NextDouble();
                npc.Level = lev;
                npc.SpaceOutfit = outfit;

                npc.EyeColor = NPCTools.GetEyeColour();
                npc.HairColor = NPCTools.GetHairColour();
                npc.SkinToneIndex = (byte)wrand.Next(8);
                npc.HeadParts.Add(NPCTools.GetHaircut(isfemale));

                npc.Items = new ExtendedList<ContainerEntry>
                {
                    new ContainerEntry() { Item = new ContainerItem() { Item = NPCTools.GetRandomGear(), Count = 1 } },
                };

                gen_quest_main.myMod.Npcs.Add(npc);
                //Add it to the list
                list.Add(npc);
                frmlst.Items.Add(npc);
            }

            gen_quest_main.myMod.FormLists.Add(frmlst);
            return gen_quest_main.myMod.FormLists[frmlst.FormKey].ToLink<IStarfieldMajorRecordGetter>();
        }
    }
}
