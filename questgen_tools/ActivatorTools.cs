using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core.Tokens;

namespace FrankyCLI.questgen_tools
{
    public class ActivatorTools
    {
        public static string GetWallModel()
        {
            Random random = new Random();

            List<string> wallmodel = new List<string>()
            {
                "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif",

            };

            return wallmodel[random.Next(wallmodel.Count)];
        }

        public static ActivatorType GetRandomSpaceType()
        {
            //We duplicate the models here but the different names give flavour
            List<ActivatorType> activatorTypes = new List<ActivatorType>()
            {
                new ActivatorType(){
                    Name = "Satellite Beacon",
                    Model = "duout\\space_base.nif"
                },
                new ActivatorType(){
                    Name = "Lost Cargo Crate",
                    Model = "duout\\space_cargo.nif"
                },
                new ActivatorType(){
                    Name = "Spaceship Engine Debris",
                    Model = "duout\\space_eng.nif"
                },
               new ActivatorType(){
                    Name = "Ejected Mech Wreckage",
                    Model = "duout\\space_mech.nif"
                },
                new ActivatorType(){
                    Name = "Remote Monitoring Beacon",
                    Model = "duout\\space_monitoring.nif"
                },
                new ActivatorType(){
                    Name = "Space Debris",
                    Model = "duout\\space_pipes.nif"
                },
                new ActivatorType(){
                    Name = "Weapon Test Site",
                    Model = "duout\\space_weapontest.nif"
                },
                new ActivatorType(){
                    Name = "Fuel Depot",
                    Model = "duout\\space_fueldepot.nif"
                },
                new ActivatorType(){
                    Name = "Hidden Storage Cache",
                    Model = "duout\\space_crimsonvault.nif"
                },
                new ActivatorType(){
                    Name = "Hydroponics Farm",
                    Model = "duout\\space_domefarm.nif"
                },
                new ActivatorType(){
                    Name = "Robotic Parts Factory",
                    Model = "duout\\space_factory.nif"
                },
                new ActivatorType(){
                    Name = "Chemical Plant",
                    Model = "duout\\space_factory2.nif"
                },
                new ActivatorType(){
                    Name = "Mechanical Systems Forge",
                    Model = "duout\\space_factory3.nif"
                },
                new ActivatorType(){
                    Name = "Orbital Processing Hub",
                    Model = "duout\\space_factory4.nif"
                },
                new ActivatorType(){
                    Name = "Interstellar Survey Node",
                    Model = "duout\\space_factory5.nif"
                },
                new ActivatorType(){
                    Name = "Far-Orbit Surveillance Array",
                    Model = "duout\\space_fuel.nif"
                },
                new ActivatorType(){
                    Name = "Deep Space Recon Sensor Grid",
                    Model = "duout\\space_generic1.nif"
                },
                new ActivatorType(){
                    Name = "Cosmic Radiation Sensor Hub",
                    Model = "duout\\space_longrangescan.nif"
                },
                new ActivatorType(){
                    Name = "Automated Ore Harvesting Platform",
                    Model = "duout\\space_mine.nif"
                },
                new ActivatorType(){
                    Name = "Microgravity Drilling Platform",
                    Model = "duout\\space_mine02.nif"
                },
                new ActivatorType(){
                    Name = "Maintenance Service Hub",
                    Model = "duout\\space_repair.nif"
                },
                new ActivatorType(){
                    Name = "Trade Authority Commerce Post",
                    Model = "duout\\space_tradeauth.nif"
                },
                new ActivatorType(){
                    Name = "Off-Grid Storage Vault",
                    Model = "duout\\space_vault.nif"
                },
            };
            Random random = new Random();
            return activatorTypes[random.Next(activatorTypes.Count)];

        }

        public static ActivatorType GetRandomDestroyGroundType()
        {
            //We duplicate the models here but the different names give flavour
            List<ActivatorType> activatorTypes = new List<ActivatorType>()
            {
                new ActivatorType(){
                    Name = "Volatile Data Core",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Hazard-Class Server Node",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Compromised System Log",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Unstable System Partition",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Critical Failure Processing Core",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Illegal Network Trace Beacon",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Corrupted Memory Lattice",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Emergency Overload Node",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Residual Hazard Signature",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Firmware Splice",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Danger-Class Cargo Crate",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Encrypted Hazard Crate",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Compromised Supply Case",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Unstable Smuggler Cache",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "High-Risk Contraband Box",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Unmarked Reactive Container",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Secured Hazard Materials Crate",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Evidence Containment Case",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Encrypted Threat Package",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Reinforced Danger Lockbox",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Sealed Hazard Transport Unit",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Unstable Fertilizer Drum",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Volatile Agrochemical Drum",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Reactive Nutrient Barrel",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Hazard-Class Soil Additive",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Contaminated Irrigation Mix",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Toxic Compost Drum",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Agrichemical Hot Zone Unit",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Bioactive Stock Barrel",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Unstable Soil Treatment Drum",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Reactive Crop Booster",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Stored Hazard Cylinder",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "High-Risk Waste Drum",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Hazard Disposal Cylinder",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Chemical Contaminant Drum",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Toxic Residue Container",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Corrosive Hazard Barrel",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Reactive Solvent Drum",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Containment Breach Barrel",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Unstable Research Waste",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Industrial Sludge Hazard",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Biohazard Overflow Unit",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },

            };
            Random random = new Random();
            return activatorTypes[random.Next(activatorTypes.Count)];
        }

        public static ActivatorType GetRandomGroundType()
        {
            //We duplicate the models here but the different names give flavour
            List<ActivatorType> activatorTypes = new List<ActivatorType>()
            {
                new ActivatorType(){
                    Name = "Data Extract",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Encrypted Server Node",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Corrupted Access Log",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Hidden System Partition",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Offline Processing Core",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Network Trace",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Fragmented Memory Cache",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Emergency Backup Node",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Residual Data Footprint",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Data Patch",
                    Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif"
                },
                new ActivatorType(){
                    Name = "Scribbled Notes",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Encoded Journal",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Folded Star Charts",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Personal Logbook",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Tattered Research Binder",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Hidden Ledger",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Smuggler Notes",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Weathered Expedition Record",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Unmarked Field Manual",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Illegal Trade Manifest",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Encrypted Coordinates File",
                    Model = "setdressing\\books\\booklarge02.nif"
                },
                new ActivatorType(){
                    Name = "Biohazard Samples",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Quarantined Specimens",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Sealed Pathogen Vials",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Contaminated Tissue Sample",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Restricted Biotech Container",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Hazardous Culture Kit",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Infected Material Case",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Confiscated Bio Agent",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Virology Research Crate",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Unstable Organic Samples",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Genetic Containment Unit",
                    Model = "setdressing\\Container\\biohazardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Secure Crate",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Encrypted Cargo Crate",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Locked Supply Case",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Smuggler Cache",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Contraband Storage Box",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Unmarked Freight Container",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Secured Materials Crate",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Evidence Lockup Case",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Encrypted Shipment Box",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Heavy Duty Lockbox",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Sealed Tech Container",
                    Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif"
                },
                new ActivatorType(){
                    Name = "Box of Scattered Documents",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Old Shipping Records",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Stack of Maintenance Logs",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Confidential Print Bundle",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Discarded Office Files",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Unsorted Paperwork Box",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Filed Research Notes",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Abandoned Work Orders",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Warehouse Inventory Sheets",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Box of Scanned Reports",
                    Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif"
                },
                new ActivatorType(){
                    Name = "Fertilizer Barrel",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Agricultural Supply Drum",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Nutrient Storage Barrel",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Soil Additive Container",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Irrigation Mix Barrel",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Processed Compost Drum",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Agrichemical Storage Unit",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Farm Stock Barrel",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Soil Treatment Barrel",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Crop Booster Drum",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Stored Nutrient Cylinder",
                    Model = "setdressing\\Container\\fertilizerbarrel_01.nif"
                },
                new ActivatorType(){
                    Name = "Industrial Waste Drum",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Hazardous Disposal Barrel",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Chemical Runoff Container",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Toxic Storage Drum",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Corrosive Material Barrel",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Discarded Solvent Drum",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Contaminant Containment Barrel",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Research Waste Cylinder",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Industrial Sludge Drum",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Biohazard Disposal Unit",
                    Model = "setdressing\\Container\\wastebarreldrum_01.nif"
                },
                new ActivatorType(){
                    Name = "Illicit Artifact Cache",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Smuggled Relic Crate",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Forbidden Cultural Items",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Unregistered Historical Goods",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Confiscated Museum Pieces",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Underground Auction Stock",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Stolen Heritage Artifacts",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Illegally Excavated Relics",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Shadow Market Curios",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Unlicensed Antiquity Bundle",
                    Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif"
                },
                new ActivatorType(){
                    Name = "Mech Components",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Illegal Servo Assemblies",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Unregistered Actuator Parts",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Restricted Mech Chassis Segments",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Smuggled Hydraulic Bundles",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Confiscated Armor Plating",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Power Couplings",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Stolen Robotics Parts",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Combat Frame Components",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Dismantled Mech Systems",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Illicit Servo Motor Crate",
                    Model = "setdressing\\contraband\\cb_mechcomponents.nif"
                },
                new ActivatorType(){
                    Name = "Sentient AI Adapters",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Restricted Neural Drivers",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Unlicensed Cognitive Modules",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Illegal AI Uplink Ports",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Blacklisted Synapse Bridges",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Smuggled Thought Processors",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Prohibited AI Integration Kits",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Cognitive Relay Interfaces",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Unregulated Sentience Nodes",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Illegal Autonomy Firmware Packs",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "AI Consciousness Bridges",
                    Model = "setdressing\\contraband\\cb_sentientaiadapters.nif"
                },
                new ActivatorType(){
                    Name = "Stolen Artwork",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Confidential File Bundle",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Classified Data Folders",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Leaked Corporate Papers",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Sensitive Contract Records",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Illicit Government Briefings",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Personnel Files",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Compromising Intelligence Reports",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Black Market Blueprint Sheets",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Intercepted Communication Logs",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Restricted Archive Copies",
                    Model = "setdressing\\contraband\\cb_stolenartwork.nif"
                },
                new ActivatorType(){
                    Name = "Illicit Biologic Samples",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Unregulated Tissue Packs",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Black Market Biomatter",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Smuggled Medical Specimens",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Donor Material",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Contraband Genetic Stock",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Bio-Specimen Transport Box",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Illegal Transplant Supplies",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Prohibited Medical Cargo",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Unregistered Biological Containers",
                    Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif"
                },
                new ActivatorType(){
                    Name = "Warfare Tech",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Experimental Combat Modules",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Weapon Prototypes",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Illicit Tactical Enhancers",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Smuggled Military Firmware",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Restricted Defense Hardware",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Blacklisted Combat Implants",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Unregulated Weapon Systems",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Prototype Battlefield Devices",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Confiscated War Lab Materials",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                },
                new ActivatorType(){
                    Name = "Advanced Targeting Components",
                    Model = "setdressing\\contraband\\cb_xenowarfaretech.nif"
                }
            };
            //


            Random random = new Random();
            return activatorTypes[random.Next(activatorTypes.Count)];
        }
    }

    public struct ActivatorType
    {
        public string Model; 
        public string Name;
    }
}
