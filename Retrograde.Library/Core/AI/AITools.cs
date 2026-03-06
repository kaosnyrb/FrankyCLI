using GameFinder.Common;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Retrograde.AI
{
    public class AITools
    {
        private static readonly ChatClient _chatClient;
        private static readonly List<ChatMessage> _history = new();

        // We can turn off the chatgpt calls for fast generation.
        public static bool AIMODE
        {
            get => RetrogradeContext.AIMode;
            set => RetrogradeContext.AIMode = value;
        }

        // When true, ExportConversation() writes the AI history to a file. Off by default.
        public static bool EXPORT_CONVERSATION = true;

        static AITools()
        {
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                         ?? throw new InvalidOperationException("OPENAI_API_KEY not set");

            var client = new OpenAIClient(apiKey);
            _chatClient = client.GetChatClient("gpt-5.1");

            // Optional: seed with a system prompt so the model knows its role
            _history.Add(new SystemChatMessage(
                GetBackgroundPrompt()
            ));
        }

        public static string TestPrompt()
        {
            string prompt = File.ReadAllText("aipromt.txt");

            string results = RunPrompt(prompt);
            Console.WriteLine(results);

            return results;
        }

        public static bool ExportConversation()
        {
            if (!EXPORT_CONVERSATION)
                return false;

            var loc = Guid.NewGuid().ToString().Substring(0, 8) + ".txt";

            string conversation = "";

            foreach (var item in _history)
            {
                if (item is UserChatMessage)
                {
                    conversation += "USER:" +  ((UserChatMessage)item).Content[0].Text.ToString();
                }
                if (item is SystemChatMessage)
                {
                    conversation += "USER:" + ((SystemChatMessage)item).Content[0].Text.ToString();
                }
                if (item is AssistantChatMessage)
                {
                    conversation += "AI:" + ((AssistantChatMessage)item).Content[0].Text.ToString();
                }
                conversation += Environment.NewLine;
            }

            try
            {
                // Create or overwrite the file with the specified content.
                File.WriteAllText(loc, conversation);

                Console.WriteLine("String successfully written to " + loc);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error writing to " + ex.Message);
            }
            return true;
        }

        // Injects text into history as a system context message without making an API call.
        // Use to surface previously-computed artefacts (e.g. PlannedArc XML) as silent context.
        public static void InjectContextIntoHistory(string content)
        {
            _history.Add(new SystemChatMessage(content));
        }

        // Makes an API call using current history as read-only context, but does NOT add messages to history.
        // Use for one-off selections (e.g. PlannedArc template picking) that should not pollute ongoing context.
        public static string RunStatelessPrompt(string prompt)
        {
            if (!AIMODE)
                return Guid.NewGuid().ToString().Substring(0, 8);

            var messages = new List<ChatMessage>(_history)
            {
                new UserChatMessage(prompt)
            };
            var res = _chatClient.CompleteChat(messages);
            string textres = res.Value.Content[0].Text;
            textres = textres.Replace(" — ", " ");
            textres = textres.Replace("—", " ");
            textres = textres.Replace("\u201c", "\"");
            textres = textres.Replace("\u201d", "\"");
            textres = textres.Replace("\u2019", "'");
            return textres;
        }

        // Make sure to set your API key in an environment variable: OPENAI_API_KEY
        public static string RunPrompt(string prompt)
        {
            //Dumb switch for fast testing
            if (!AIMODE)
            {
                return Guid.NewGuid().ToString().Substring(0, 8);
            }

            // 1. Add user message to history
            _history.Add(new UserChatMessage(prompt));

            // 2. Call the model with the full history
            var res = _chatClient.CompleteChat(_history);

            // 3. Get the assistant’s reply text
            string textres = res.Value.Content[0].Text;

            // 4. Do your existing cleanup
            textres = textres.Replace(" — ", " "); // No EM-dashes with spaces
            textres = textres.Replace("—", " ");    // No EM-dashes

            // 5. Add assistant reply back into history so context is preserved
            _history.Add(new AssistantChatMessage(textres));

            return textres;
        }



        public static string GetBackgroundPrompt()
        {
            string result = "";
            result += "You are a Starfield quest writer crafting an in-game mission narrative.\r\n\r\n";

            result += "Include newline characters in your response if there are multiple sentences.\r\n";
            result += "Don’t use the following characters: —\r\n";

            result += "The following is background information about the Starfield universe. Do not quote it directly.\r\n\r\n";

            // Factions
            result += "The United Colonies (UC) is the largest human government — a centralized republic with its capital at New Atlantis on Jemison. The UC military includes the Navy, Vanguard (privateer arm), Marines, and UC SysDef (anti-piracy).\r\n";
            result += "The Freestar Collective is a libertarian confederation of three systems (Cheyenne, Volii, Narion) with its capital at Akila City. Its law enforcement arms are Freestar Security and the elite Freestar Rangers, headquartered at The Rock in Akila City.\r\n";
            result += "The UC and Freestar fought the Colony War (ended 2311 with the Armistice); an uneasy peace holds in 2330.\r\n";
            result += "The Crimson Fleet is a loose coalition of space pirates, expanded far beyond their Kryx system origin by 2330 and now a recognized threat across the Settled Systems.\r\n";
            result += "House Va’ruun is a fanatical theocracy worshipping the Great Serpent; Va’ruun Zealots remain hostile across the Settled Systems after House Va’ruun withdrew into isolation.\r\n";
            result += "Ecliptic Mercenaries are guns-for-hire widely used for dirty work by all factions. Spacers are unaffiliated scavengers and criminals with no central leadership.\r\n";
            result += "The Trackers Alliance posts bounties and runs clearance services across the Settled Systems.\r\n";

            // Key locations (name + role only)
            result += "Key locations: New Atlantis (UC capital, Jemison) — districts: MAST, Residential, Commercial, The Well, Spaceport. Neon (pleasure city on Volii Alpha, Freestar) — districts: Neon Core, Ebbside, Underbelly, Rooftops, Starport; controlled by Xenofresh Fisheries, home of Ryujin Industries. Akila City (Freestar capital, Akila) — districts: The Core, Midtown, Coe Plaza, The Stretch, Spaceport, City Walls, Farms. Gagarin Landing (industrial settlement, planet Gagarin) — recovering post-Armistice economy. Cydonia (UC mining hub, Mars). The Den (neutral space station). The Clinic (Freestar medical station). Paradiso (luxury resort, Porrima II). Red Mile (dangerous blood sport outpost, Porrima III). HopeTown (HopeTech shipyard, Polvo). New Homestead (historic settlement, Titan). Waggoner Farm (small farm, Montara Luna).\r\n";

            // Key corporations (name + role only)
            result += "Key corporations: GalBank (finance, ATMs everywhere), Trade Authority (gray-market traders who buy contraband), Reliant Medical (largest medical provider), Argos Extractors (mining), Ryujin Industries (megacorp, Neon), Xenofresh Fisheries (owns Neon, produces Aurora drug), HopeTech (cargo ships, HopeTown), Red Harvest (grain products), Centauri Mills (food manufacturing), LIST (settler support org for fringe worlds).\r\n";

            result += "The game starts on May 7th 2330. Use this to figure out any dates needed.\r\n\r\n";

            result += "This marks the end of the background information section. Following this is more detail on the prompt to carry out.\r\n\r\n";

            result += "Quest generation structure — follow these rules for all story content you generate:\r\n";
            result += "- Story stages are generated from the final encounter backwards: showdown first, then investigations, then discovery.\r\n";
            result += "- Each earlier stage must reveal progressively less about the target.\r\n";
            result += "- Use details from each previously generated stage to inform the next.\r\n";
            result += "- New <Lore> entries will appear during generation; use them to enrich each scene.\r\n";
            result += "- At least one concrete lore detail (faction, tech, location, rumor) must ground each scene.\r\n";
            result += "- Ensure all stages link together logically across the full chain.\r\n\r\n";

            return result;
        }

        public static string GetStarfieldBasicWeaponsLoreList()
        {
            var sb = new StringBuilder();

            sb.AppendLine("These entries represent Starfield’s basic and widely circulated weapons. Each is given a five-word lore descriptor to help prompts model combat identity, faction usage, frontier trade patterns, and narrative flavor around weapon provenance and battlefield presence.");

            // Ballistic Pistols
            sb.AppendLine("Equinox - Stable energy pistol favored by explorers");
            sb.AppendLine("Eon - Compact sidearm built for reliability");
            sb.AppendLine("Magshot - Magnetic revolver delivering brutal impact");
            sb.AppendLine("Sidestar - Lightweight civilian sidearm turned lethal");
            sb.AppendLine("Urban Eagle - Heavy pistol symbolizing frontier authority");

            // Ballistic Rifles
            sb.AppendLine("Beowulf - Precision rifle engineered for versatility");
            sb.AppendLine("Coachman - Double-barrel shotgun firing devastating bursts");
            sb.AppendLine("Grendel - Standard-issue rifle trusted by militias");
            sb.AppendLine("Old Earth Assault Rifle - Classic firearm preserved across centuries");
            sb.AppendLine("Old Earth Hunting Rifle - Heritage rifle used for long-range strikes");

            // Ballistic & Heavy Variants
            sb.AppendLine("Bridger - Grenade launcher offering controlled destruction");
            sb.AppendLine("Drum Beat - Rapid-fire rifle dominating close quarters");
            sb.AppendLine("Lawgiver - Frontier rifle valued for rugged performance");

            // Laser Weapons
            sb.AppendLine("Solstice - Reliable laser pistol common among settlers");
            sb.AppendLine("Equinox Rifle - Mid-range laser weapon balancing accuracy");

            // Particle Beam Weapons
            sb.AppendLine("Orion - Particle beam rifle with surgical lethality");
            sb.AppendLine("Va'ruun Inflictor - Cult-forged weapon channeling focused devastation");
            sb.AppendLine("Va'ruun Starshard - Exotic emitter harming both flesh and faith");
            sb.AppendLine("Novalight - Precision beam pistol cutting molecular bonds");

            // Mag Weapons
            sb.AppendLine("Magpulse - Electromagnetic rifle firing kinetic bolts");
            sb.AppendLine("Magstorm - Heavy SMG unleashing suppressive magnetic fire");
            sb.AppendLine("Magshot - Magnetic revolver delivering high-velocity rounds");

            // Shotguns
            sb.AppendLine("Breach - Tactical shotgun shattering armored defenses");
            sb.AppendLine("Coachman - Classic double-barrel delivering overwhelming force");

            // Explosives
            sb.AppendLine("Negotiator - Tube-launched warhead resolving disputes explosively");
            sb.AppendLine("Frag Grenade - Fragmentation charge unfolding lethal shrapnel");
            sb.AppendLine("Pulse Grenade - EMP device crippling electronic systems");
            sb.AppendLine("Thermite Grenade - Intense incendiary melting through defenses");

            // Melee Weapons
            sb.AppendLine("Barrow Knife - Rugged survival blade forged for frontier");
            sb.AppendLine("Combat Knife - Standard military blade ending skirmishes");
            sb.AppendLine("Osmium Dagger - Dense alloy blade piercing armor");
            sb.AppendLine("Crucible Blade - Ceremonial sword retaining deadly purpose");
            sb.AppendLine("Ripshank - Improvised shiv wielded by desperate outlaws");

            //Digipicks
            sb.AppendLine("Digipick - Precision cryptographic tool used to bypass digital locks and defeat security systems.");

            return sb.ToString();
        }


        public static string GetStarfieldResourceLoreList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("The following entries represent Starfield’s known crafting and trade resources, each described in five words to establish tone and function; the prompt may use them as worldbuilding anchors for economy, faction trade routes, scarcity themes, smuggling hooks, environmental storytelling, or any lore-driven interpretation of resource value and usage.");

            sb.AppendLine("Amino Acids - Fundamental compounds sustaining alien life");
            sb.AppendLine("Aqueous Hematite - Mineral-rich fluid from oxidized worlds");
            sb.AppendLine("Chlorine - Sharp reactive gas for purification");
            sb.AppendLine("Contaminated Water - Polluted runoff from compromised systems");
            sb.AppendLine("Fiber - Tough strands harvested from vegetation");
            sb.AppendLine("Fluorine - Corrosive element prized for reactions");
            sb.AppendLine("Iron - Common metal shaping industrial foundations");
            sb.AppendLine("Membrane - Flexible biofilm from hardy organisms");
            sb.AppendLine("Nutrient - Compressed sustenance from cultivated biomass");
            sb.AppendLine("Polymer - Synthetic chains forming durable structures");
            sb.AppendLine("Toxin - Potent biological agent requiring caution");
            sb.AppendLine("Water - Essential liquid threaded through ecosystems");

            sb.AppendLine("Aluminum - Lightweight metal used in alloys");
            sb.AppendLine("Antimony - Brittle metalloid enhancing exotic compounds");
            sb.AppendLine("Beryllium - Rare metal powering advanced reactors");
            sb.AppendLine("Caesium - Volatile alkali metal enabling sensors");
            sb.AppendLine("Cobalt - Magnetic metal strengthening refined materials");
            sb.AppendLine("Copper - Conductive metal vital for circuitry");
            sb.AppendLine("Europium - Luminous rare earth for systems");
            sb.AppendLine("Gold - Noble metal stabilizing precision electronics");
            sb.AppendLine("Iridium - Dense metal resisting extreme conditions");
            sb.AppendLine("Lead - Heavy metal shielding hazardous emissions");
            sb.AppendLine("Lithium - Reactive metal fueling energy storage");
            sb.AppendLine("Mercury - Liquid metal enabling specialized instrumentation");
            sb.AppendLine("Neodymium - Strong magnetic element powering drives");
            sb.AppendLine("Nickel - Durable alloy metal for mechanisms");
            sb.AppendLine("Palladium - Rare catalyst essential for refinement");
            sb.AppendLine("Platinum - High-value metal resisting corrosion");
            sb.AppendLine("Plutonium - Radioactive fuel for deep operations");
            sb.AppendLine("Ruthenium - Catalytic rare metal enhancing reactions");
            sb.AppendLine("Silver - Purified conductor improving sensitive electronics");
            sb.AppendLine("Tantalum - Heat-resistant metal for advanced fabrication");
            sb.AppendLine("Tin - Soft metal binding industrial alloys");
            sb.AppendLine("Titanium - Strong lightweight metal for construction");
            sb.AppendLine("Tungsten - Ultra-dense metal surviving extreme heat");
            sb.AppendLine("Vanadium - Alloying element boosting structural integrity");

            sb.AppendLine("Helium-3 - Fusion-reactive isotope sustaining reactors");
            sb.AppendLine("Hydrogen - Basic element fueling stellar technology");
            sb.AppendLine("Neon - Inert gas used in illumination");
            sb.AppendLine("Argon - Stable gas protecting sensitive reactions");
            sb.AppendLine("Xenon - Heavy inert gas for propulsion");

            sb.AppendLine("Adhesive - Bonding compound for versatile assembly");
            sb.AppendLine("Aldumite - Rare crystalline material strengthening components");
            sb.AppendLine("Aneutronic Fusion Cell - High-output cell driving compact reactors");
            sb.AppendLine("Aromatic - Organic compounds forming specialized solutions");
            sb.AppendLine("Biosuppressant - Agent neutralizing biological contamination threats");
            sb.AppendLine("Caelumite - Unknown crystalline energy-bearing shard");
            sb.AppendLine("Cosmetic - Nonessential compounds refining surface appearance");
            sb.AppendLine("Drilling Rig - Compact system for material extraction");
            sb.AppendLine("Hijacker Chip - Signal override chip defeating locks");
            sb.AppendLine("Immunostimulant - Enhancer boosting resilience under stress");
            sb.AppendLine("Isocentered Magnet - Precision magnet stabilizing delicate machinery");
            sb.AppendLine("Lubricant - Fluid reducing mechanical friction");
            sb.AppendLine("Microcell - Miniature power module for devices");
            sb.AppendLine("Neuroamp - Neural amplifier enhancing signal flow");
            sb.AppendLine("Ornamental - Decorative element showcasing refined craftsmanship");
            sb.AppendLine("Pigment - Colorants extracted for diverse applications");
            sb.AppendLine("Reactive Gauge - Sensor tracking environmental fluctuations");
            sb.AppendLine("Sealant - Protective layer preventing material degradation");
            sb.AppendLine("Solvent - Reactive liquid dissolving complex compounds");
            sb.AppendLine("Sterile Nanotubes - Clean microstructures enabling advanced fabrication");
            sb.AppendLine("Structural - Reinforcement material improving system durability");
            sb.AppendLine("Zero Wire - Conductor carrying ultra-stable signals");

            return sb.ToString();
        }

        public static string GetStarfieldMedicalLoreList()
        {
            var sb = new StringBuilder();

            sb.AppendLine("These entries represent Starfield’s medical, pharmaceutical, and illicit chem items, each described in five words to help prompts model trade value, scarcity, black-market demand, faction smuggling routes, medical usage, and environmental storytelling within the Settled Systems.");

            sb.AppendLine("Med Pack - Standard emergency kit restoring vitality");
            sb.AppendLine("Trauma Pack - Advanced stabilizer for severe injuries");
            sb.AppendLine("Emergency Kit - Comprehensive field treatment supplies");
            sb.AppendLine("Healing Salve - Organic compound accelerating natural recovery");

            sb.AppendLine("Med-X - Potent analgesic boosting pain resistance");
            sb.AppendLine("BattleUp - Stimulant increasing short-term physical performance");
            sb.AppendLine("Pick-Me-Up - Mild stimulant restoring combat readiness");
            sb.AppendLine("Red Trench - Illicit chem heightening sensory response");
            sb.AppendLine("Penicillin-X - Broad-spectrum antibiotic neutralizing infections");
            sb.AppendLine("Paramed-X - Quick-acting medical enhancer boosting resilience");

            sb.AppendLine("Addictol - Chemical suppressant reducing dependency symptoms");
            sb.AppendLine("Antibiotic - Standard medication eliminating harmful pathogens");
            sb.AppendLine("Antimicrobial - Compound targeting resistant biological contaminants");
            sb.AppendLine("Antiseptic - Disinfectant preventing infection during injuries");

            sb.AppendLine("Boosted Shot - Combat stim enhancing reflexive responsiveness");
            sb.AppendLine("Frostwolf - Illegal chem inducing controlled hypothermic calm");
            sb.AppendLine("Mind Control - Cognitive agent altering perception patterns");
            sb.AppendLine("Snake Oil - Dubious remedy promising miraculous recovery");

            sb.AppendLine("Infused Bandage - Treated wrap accelerating wound closure");
            sb.AppendLine("Regulator - Drug stabilizing erratic physiological responses");
            sb.AppendLine("Stimulant - Broad enhancer increasing alertness and drive");
            sb.AppendLine("Depressant - Chemical agent suppressing neural activity");

            sb.AppendLine("Sedative - Calming compound reducing panic symptoms");
            sb.AppendLine("Amp - Compact injector providing rapid stimulation");
            sb.AppendLine("Expired Meds - Unstable pharmaceuticals with unreliable effects");
            sb.AppendLine("Steroid Pack - Muscle-boosting compound enhancing temporary strength");

            sb.AppendLine("Heart+ - Cardiovascular booster supporting sustained exertion");
            sb.AppendLine("Lung+ - Pulmonary enhancer improving oxygen efficiency");
            sb.AppendLine("Neuro+ - Neural booster sharpening cognitive processing");
            sb.AppendLine("Mind+ - Focus enhancer suppressing environmental distractions");

            sb.AppendLine("Snakebite - Street chem causing dangerous adrenaline surges");
            sb.AppendLine("Aurora - Euphoric Neon-exclusive narcotic enhancing perception");
            sb.AppendLine("Universal Solvent - Harsh chemical with medical-adjacent applications");
            sb.AppendLine("Digipack Meds - Compact med-case supporting field operatives");

            return sb.ToString();
        }


    }
}
