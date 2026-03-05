using System;
using System.Collections.Generic;

namespace Retrograde.Utils;

/// <summary>
/// Provides random activator type selections for space objects and ground items.
/// </summary>
public static class ActivatorSeedData
{
    public static string GetWallModel()
    {
        var random = RandomProvider.Random;

        List<string> wallmodel = new List<string>()
        {
            "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif",
        };

        return wallmodel[random.Next(wallmodel.Count)];
    }

    public static ActivatorType GetRandomSpaceType()
    {
        List<ActivatorType> activatorTypes = new List<ActivatorType>()
        {
            new ActivatorType() { Name = "Satellite Beacon", Model = "duout\\space_base.nif" },
            new ActivatorType() { Name = "Lost Cargo Crate", Model = "duout\\space_cargo.nif" },
            new ActivatorType() { Name = "Spaceship Engine Debris", Model = "duout\\space_eng.nif" },
            new ActivatorType() { Name = "Ejected Mech Wreckage", Model = "duout\\space_mech.nif" },
            new ActivatorType() { Name = "Remote Monitoring Beacon", Model = "duout\\space_monitoring.nif" },
            new ActivatorType() { Name = "Space Debris", Model = "duout\\space_pipes.nif" },
            new ActivatorType() { Name = "Weapon Test Site", Model = "duout\\space_weapontest.nif" },
            new ActivatorType() { Name = "Fuel Depot", Model = "duout\\space_fueldepot.nif" },
            new ActivatorType() { Name = "Hidden Storage Cache", Model = "duout\\space_crimsonvault.nif" },
            new ActivatorType() { Name = "Hydroponics Farm", Model = "duout\\space_domefarm.nif" },
            new ActivatorType() { Name = "Robotic Parts Factory", Model = "duout\\space_factory.nif" },
            new ActivatorType() { Name = "Chemical Plant", Model = "duout\\space_factory2.nif" },
            new ActivatorType() { Name = "Mechanical Systems Forge", Model = "duout\\space_factory3.nif" },
            new ActivatorType() { Name = "Orbital Processing Hub", Model = "duout\\space_factory4.nif" },
            new ActivatorType() { Name = "Interstellar Survey Node", Model = "duout\\space_factory5.nif" },
            new ActivatorType() { Name = "Far-Orbit Surveillance Array", Model = "duout\\space_fuel.nif" },
            new ActivatorType() { Name = "Deep Space Recon Sensor Grid", Model = "duout\\space_generic1.nif" },
            new ActivatorType() { Name = "Cosmic Radiation Sensor Hub", Model = "duout\\space_longrangescan.nif" },
            new ActivatorType() { Name = "Automated Ore Harvesting Platform", Model = "duout\\space_mine.nif" },
            new ActivatorType() { Name = "Microgravity Drilling Platform", Model = "duout\\space_mine02.nif" },
            new ActivatorType() { Name = "Maintenance Service Hub", Model = "duout\\space_repair.nif" },
            new ActivatorType() { Name = "Trade Authority Commerce Post", Model = "duout\\space_tradeauth.nif" },
            new ActivatorType() { Name = "Off-Grid Storage Vault", Model = "duout\\space_vault.nif" },
        };
        var random = RandomProvider.Random;
        return activatorTypes[random.Next(activatorTypes.Count)];
    }

    public static ActivatorType GetRandomDestroyGroundType()
    {
        List<ActivatorType> activatorTypes = new List<ActivatorType>()
        {
            new ActivatorType() { Name = "Volatile Data Core", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Hazard-Class Server Node", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Compromised System Log", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Unstable System Partition", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Critical Failure Processing Core", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Danger-Class Cargo Crate", Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif" },
            new ActivatorType() { Name = "Encrypted Hazard Crate", Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif" },
            new ActivatorType() { Name = "Unstable Fertilizer Drum", Model = "setdressing\\Container\\fertilizerbarrel_01.nif" },
            new ActivatorType() { Name = "Volatile Agrochemical Drum", Model = "setdressing\\Container\\fertilizerbarrel_01.nif" },
            new ActivatorType() { Name = "High-Risk Waste Drum", Model = "setdressing\\Container\\wastebarreldrum_01.nif" },
            new ActivatorType() { Name = "Hazard Disposal Cylinder", Model = "setdressing\\Container\\wastebarreldrum_01.nif" },
        };
        var random = RandomProvider.Random;
        return activatorTypes[random.Next(activatorTypes.Count)];
    }

    public static ActivatorType GetRandomLargeGroundType()
    {
        List<ActivatorType> activatorTypes = new List<ActivatorType>()
        {
            new ActivatorType() { Name = "Computer Core", Model = "duout\\large_scanner.nif" },
            new ActivatorType() { Name = "Contraband", Model = "duout\\large_contraband.nif" },
            new ActivatorType() { Name = "Chemicals", Model = "duout\\large_chemical.nif" },
        };
        var random = RandomProvider.Random;
        return activatorTypes[random.Next(activatorTypes.Count)];
    }

    public static ActivatorType GetRandomGroundType()
    {
        List<ActivatorType> activatorTypes = new List<ActivatorType>()
        {
            // Data/Computer types
            new ActivatorType() { Name = "Data Extract", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Encrypted Server Node", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Corrupted Access Log", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Hidden System Partition", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            new ActivatorType() { Name = "Offline Processing Core", Model = "SetDressing\\Computer_Cabinets\\Computer_Cabinet_Base_Computer01.nif" },
            // Book types
            new ActivatorType() { Name = "Scribbled Notes", Model = "setdressing\\books\\booklarge02.nif" },
            new ActivatorType() { Name = "Encoded Journal", Model = "setdressing\\books\\booklarge02.nif" },
            new ActivatorType() { Name = "Folded Star Charts", Model = "setdressing\\books\\booklarge02.nif" },
            new ActivatorType() { Name = "Personal Logbook", Model = "setdressing\\books\\booklarge02.nif" },
            // Biohazard types
            new ActivatorType() { Name = "Biohazard Samples", Model = "setdressing\\Container\\biohazardbox01.nif" },
            new ActivatorType() { Name = "Quarantined Specimens", Model = "setdressing\\Container\\biohazardbox01.nif" },
            new ActivatorType() { Name = "Sealed Pathogen Vials", Model = "setdressing\\Container\\biohazardbox01.nif" },
            // Crate types
            new ActivatorType() { Name = "Secure Crate", Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif" },
            new ActivatorType() { Name = "Encrypted Cargo Crate", Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif" },
            new ActivatorType() { Name = "Smuggler Cache", Model = "SetDressing\\ConsolePanels\\ConsoleCrateA02.nif" },
            // Cardboard box types
            new ActivatorType() { Name = "Box of Scattered Documents", Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif" },
            new ActivatorType() { Name = "Old Shipping Records", Model = "setdressing\\crates\\cardboardboxes\\cardboardbox01.nif" },
            // Barrel types
            new ActivatorType() { Name = "Fertilizer Barrel", Model = "setdressing\\Container\\fertilizerbarrel_01.nif" },
            new ActivatorType() { Name = "Industrial Waste Drum", Model = "setdressing\\Container\\wastebarreldrum_01.nif" },
            // Contraband types
            new ActivatorType() { Name = "Illicit Artifact Cache", Model = "setdressing\\contraband\\cb_blackmarketantiquities.nif" },
            new ActivatorType() { Name = "Mech Components", Model = "setdressing\\contraband\\cb_mechcomponents.nif" },
            new ActivatorType() { Name = "Sentient AI Adapters", Model = "setdressing\\contraband\\cb_sentientaiadapters.nif" },
            new ActivatorType() { Name = "Stolen Artwork", Model = "setdressing\\contraband\\cb_stolenartwork.nif" },
            new ActivatorType() { Name = "Illicit Biologic Samples", Model = "setdressing\\contraband\\cb_unethicallyharvestedorgans.nif" },
            new ActivatorType() { Name = "Warfare Tech", Model = "setdressing\\contraband\\cb_xenowarfaretech.nif" },
        };

        var random = RandomProvider.Random;
        return activatorTypes[random.Next(activatorTypes.Count)];
    }
}

public struct ActivatorType
{
    public string Model;
    public string Name;
}
