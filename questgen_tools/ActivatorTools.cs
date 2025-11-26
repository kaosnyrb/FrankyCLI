using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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


        public static ActivatorType GetRandomGroundType()
        {
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
                    Name = "Encrypted Hackbook",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Stolen Access Terminal",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Cracked Security Laptop",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Portable Intrusion Rig",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Spoofed Credential Machine",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Hidden Cyberdeck",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Signal Scrambler Console",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Contraband Data Laptop",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Unauthorized Network Device",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                },
                new ActivatorType(){
                    Name = "Remote Access Terminal",
                    Model = "SetDressing\\Hackers_LapTop\\Hackers_Laptop_01.nif"
                }
                //
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
