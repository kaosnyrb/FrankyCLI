using System.Text;

namespace Retrograde.AI.Utils
{
    public static class AISeedData
    {
        public static string GetBackgroundPrompt()
        {
            string result = "";
            result += "You are a Starfield quest designer writing in-game content (logs, dialogue, objectives).\r\n\r\n";

            result += "Writing rules — apply to ALL generated text:\r\n";
            result += "- Write like field intel: terse, factual, concrete. State who did what, where, and why.\r\n";
            result += "- No metaphors, similes, or atmospheric filler. No adjectives unless they are physical descriptions.\r\n";
            result += "- Never use: 'mysterious', 'ominous', 'desperate', 'shadows', 'whispers', 'echoes', 'lurking', 'haunting'.\r\n";
            result += "- Every sentence must contain at least one proper noun (name, location, faction, or item).\r\n";
            result += "- Include newline characters in your response if there are multiple sentences.\r\n";
            result += "- Don't use the following characters: —\r\n";

            result += "The following is background information about the Starfield universe. Do not quote it directly.\r\n\r\n";

            // Factions
            result += "The United Colonies (UC) is the largest human government — a centralized republic with its capital at New Atlantis on Jemison. The UC military includes the Navy, Vanguard (privateer arm), Marines, and UC SysDef (anti-piracy).\r\n";
            result += "The Freestar Collective is a libertarian confederation of three systems (Cheyenne, Volii, Narion) with its capital at Akila City. Its law enforcement arms are Freestar Security and the elite Freestar Rangers, headquartered at The Rock in Akila City.\r\n";
            result += "The UC and Freestar fought the Colony War (ended 2311 with the Armistice); an uneasy peace holds in 2330.\r\n";
            result += "The Crimson Fleet is a loose coalition of space pirates, expanded far beyond their Kryx system origin by 2330 and now a recognized threat across the Settled Systems.\r\n";
            result += "House Va'ruun is a fanatical theocracy worshipping the Great Serpent; Va'ruun Zealots remain hostile across the Settled Systems after House Va'ruun withdrew into isolation.\r\n";
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
            result += "- New <Lore> entries will appear during generation; use them to ground each scene in concrete, specific details. Prefer facts over atmosphere. State what happened, who was involved, and where.\r\n";
            result += "- At least one concrete lore detail (faction, tech, location, rumor) must ground each scene.\r\n";
            result += "- Ensure all stages link together logically across the full chain.\r\n\r\n";

            return result;
        }

        public static string GetStarfieldBasicWeaponsLoreList()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Starfield weapons reference. Use for trade, combat, and faction context.");

            // Ballistic Pistols
            sb.AppendLine("Equinox - energy pistol, standard settler sidearm");
            sb.AppendLine("Eon - compact ballistic pistol, mass-produced");
            sb.AppendLine("Magshot - magnetic revolver, heavy caliber");
            sb.AppendLine("Sidestar - lightweight civilian pistol, common");
            sb.AppendLine("Urban Eagle - large-frame pistol, law enforcement issue");

            // Ballistic Rifles
            sb.AppendLine("Beowulf - semi-auto rifle, modular platform");
            sb.AppendLine("Coachman - double-barrel shotgun, short range");
            sb.AppendLine("Grendel - standard-issue rifle, militia and UC");
            sb.AppendLine("Old Earth Assault Rifle - pre-settlement automatic rifle");
            sb.AppendLine("Old Earth Hunting Rifle - pre-settlement bolt-action");

            // Ballistic & Heavy Variants
            sb.AppendLine("Bridger - single-shot grenade launcher");
            sb.AppendLine("Drum Beat - high-rate-of-fire automatic rifle");
            sb.AppendLine("Lawgiver - lever-action rifle, frontier standard");

            // Laser Weapons
            sb.AppendLine("Solstice - laser pistol, common civilian model");
            sb.AppendLine("Equinox Rifle - laser rifle, mid-range");

            // Particle Beam Weapons
            sb.AppendLine("Orion - particle beam rifle, high accuracy");
            sb.AppendLine("Va'ruun Inflictor - particle weapon, Va'ruun manufacture");
            sb.AppendLine("Va'ruun Starshard - particle emitter, Va'ruun manufacture");
            sb.AppendLine("Novalight - particle beam pistol, precision model");

            // Mag Weapons
            sb.AppendLine("Magpulse - electromagnetic rifle, kinetic rounds");
            sb.AppendLine("Magstorm - magnetic SMG, high volume of fire");
            sb.AppendLine("Magshot - magnetic revolver, heavy rounds");

            // Shotguns
            sb.AppendLine("Breach - pump-action shotgun, tactical model");
            sb.AppendLine("Coachman - double-barrel shotgun, close quarters");

            // Explosives
            sb.AppendLine("Negotiator - shoulder-fired rocket launcher");
            sb.AppendLine("Frag Grenade - standard fragmentation grenade");
            sb.AppendLine("Pulse Grenade - EMP grenade, disables electronics");
            sb.AppendLine("Thermite Grenade - incendiary grenade, burns hot");

            // Melee Weapons
            sb.AppendLine("Barrow Knife - utility knife, frontier issue");
            sb.AppendLine("Combat Knife - military knife, standard issue");
            sb.AppendLine("Osmium Dagger - heavy alloy blade, armor-piercing");
            sb.AppendLine("Crucible Blade - ceremonial sword, still functional");
            sb.AppendLine("Ripshank - improvised blade, common among spacers");

            sb.AppendLine("Digipick - lockpicking tool, bypasses digital locks");

            return sb.ToString();
        }

        public static string GetStarfieldResourceLoreList()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Starfield crafting and trade resources. Use for economy, trade routes, and scarcity context.");

            sb.AppendLine("Amino Acids - organic compound, biological applications");
            sb.AppendLine("Aqueous Hematite - iron-bearing mineral, extracted from water");
            sb.AppendLine("Chlorine - reactive gas, water purification");
            sb.AppendLine("Contaminated Water - polluted water, requires processing");
            sb.AppendLine("Fiber - plant-derived material, textile and composite use");
            sb.AppendLine("Fluorine - reactive element, industrial chemical");
            sb.AppendLine("Iron - common metal, structural and industrial");
            sb.AppendLine("Membrane - biological film, filtration applications");
            sb.AppendLine("Nutrient - processed food compound, colony supply");
            sb.AppendLine("Polymer - synthetic material, manufacturing base");
            sb.AppendLine("Toxin - biological agent, hazardous, restricted");
            sb.AppendLine("Water - essential resource, colony critical");

            sb.AppendLine("Aluminum - lightweight structural metal");
            sb.AppendLine("Antimony - metalloid, compound manufacturing");
            sb.AppendLine("Beryllium - reactor component metal, scarce");
            sb.AppendLine("Caesium - alkali metal, sensor manufacturing");
            sb.AppendLine("Cobalt - magnetic metal, alloy component");
            sb.AppendLine("Copper - conductive metal, wiring and circuitry");
            sb.AppendLine("Europium - rare earth, display and sensor tech");
            sb.AppendLine("Gold - conductor, precision electronics");
            sb.AppendLine("Iridium - dense metal, high-stress applications");
            sb.AppendLine("Lead - heavy metal, radiation shielding");
            sb.AppendLine("Lithium - battery and energy storage metal");
            sb.AppendLine("Mercury - liquid metal, instrument manufacturing");
            sb.AppendLine("Neodymium - magnetic element, drive components");
            sb.AppendLine("Nickel - alloy metal, mechanical parts");
            sb.AppendLine("Palladium - catalyst metal, refining processes");
            sb.AppendLine("Platinum - corrosion-resistant, high-value trade");
            sb.AppendLine("Plutonium - radioactive fuel, restricted");
            sb.AppendLine("Ruthenium - catalyst metal, chemical processing");
            sb.AppendLine("Silver - conductor, electronics component");
            sb.AppendLine("Tantalum - heat-resistant metal, advanced fabrication");
            sb.AppendLine("Tin - soft metal, alloy component");
            sb.AppendLine("Titanium - lightweight structural metal, construction");
            sb.AppendLine("Tungsten - dense metal, high-heat applications");
            sb.AppendLine("Vanadium - alloy additive, structural reinforcement");

            sb.AppendLine("Helium-3 - fusion fuel isotope, reactor grade");
            sb.AppendLine("Hydrogen - basic fuel element, widespread");
            sb.AppendLine("Neon - inert gas, lighting and signage");
            sb.AppendLine("Argon - inert gas, welding and manufacturing");
            sb.AppendLine("Xenon - heavy inert gas, ion propulsion");

            sb.AppendLine("Adhesive - bonding compound, general assembly");
            sb.AppendLine("Aldumite - rare crystal, component hardening");
            sb.AppendLine("Aneutronic Fusion Cell - compact reactor power cell");
            sb.AppendLine("Aromatic - organic compound, chemical synthesis");
            sb.AppendLine("Biosuppressant - biological containment agent");
            sb.AppendLine("Caelumite - unknown crystal, energy properties");
            sb.AppendLine("Cosmetic - surface treatment compound");
            sb.AppendLine("Drilling Rig - portable extraction equipment");
            sb.AppendLine("Hijacker Chip - signal override device, restricted");
            sb.AppendLine("Immunostimulant - immune system booster, medical");
            sb.AppendLine("Isocentered Magnet - precision magnet, calibration use");
            sb.AppendLine("Lubricant - mechanical friction reducer");
            sb.AppendLine("Microcell - miniature power module");
            sb.AppendLine("Neuroamp - neural signal amplifier, medical");
            sb.AppendLine("Ornamental - decorative material, non-functional");
            sb.AppendLine("Pigment - coloring agent, industrial");
            sb.AppendLine("Reactive Gauge - environmental sensor component");
            sb.AppendLine("Sealant - protective coating, hull and pipe repair");
            sb.AppendLine("Solvent - chemical dissolver, industrial");
            sb.AppendLine("Sterile Nanotubes - medical-grade microstructures");
            sb.AppendLine("Structural - reinforcement material, construction");
            sb.AppendLine("Zero Wire - ultra-stable signal conductor");

            return sb.ToString();
        }

        public static string GetStarfieldMedicalLoreList()
        {
            var sb = new StringBuilder();

            sb.AppendLine("Starfield medical and pharmaceutical items. Use for trade, medical supply, and contraband context.");

            sb.AppendLine("Med Pack - standard first aid kit");
            sb.AppendLine("Trauma Pack - field stabilizer, severe injuries");
            sb.AppendLine("Emergency Kit - comprehensive medical supplies");
            sb.AppendLine("Healing Salve - topical wound treatment");

            sb.AppendLine("Med-X - painkiller, prescription strength");
            sb.AppendLine("BattleUp - combat stimulant, short-duration");
            sb.AppendLine("Pick-Me-Up - mild stimulant, over-the-counter");
            sb.AppendLine("Red Trench - illegal stimulant, controlled substance");
            sb.AppendLine("Penicillin-X - broad-spectrum antibiotic");
            sb.AppendLine("Paramed-X - emergency medical booster");

            sb.AppendLine("Addictol - addiction treatment medication");
            sb.AppendLine("Antibiotic - standard anti-infection medication");
            sb.AppendLine("Antimicrobial - resistant-strain treatment");
            sb.AppendLine("Antiseptic - wound disinfectant");

            sb.AppendLine("Boosted Shot - combat performance stimulant");
            sb.AppendLine("Frostwolf - illegal sedative, controlled substance");
            sb.AppendLine("Mind Control - cognitive-altering chem, restricted");
            sb.AppendLine("Snake Oil - unregulated remedy, dubious efficacy");

            sb.AppendLine("Infused Bandage - treated wound dressing");
            sb.AppendLine("Regulator - metabolic stabilizer");
            sb.AppendLine("Stimulant - general alertness booster");
            sb.AppendLine("Depressant - neural suppressant, prescription");

            sb.AppendLine("Sedative - calming agent, medical use");
            sb.AppendLine("Amp - rapid-delivery stimulant injector");
            sb.AppendLine("Expired Meds - degraded pharmaceuticals, unreliable");
            sb.AppendLine("Steroid Pack - muscle-performance booster, short-term");

            sb.AppendLine("Heart+ - cardiovascular performance supplement");
            sb.AppendLine("Lung+ - respiratory efficiency supplement");
            sb.AppendLine("Neuro+ - cognitive performance supplement");
            sb.AppendLine("Mind+ - focus and concentration supplement");

            sb.AppendLine("Snakebite - street stimulant, dangerous, unregulated");
            sb.AppendLine("Aurora - synthetic narcotic, Neon-exclusive, controlled");
            sb.AppendLine("Universal Solvent - industrial chemical, medical-adjacent");
            sb.AppendLine("Digipack Meds - compact field medical kit");

            return sb.ToString();
        }
    }
}
