using Retrograde.Utils;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using System.Collections.Generic;

namespace Retrograde.Quests
{
    public class Templates_Discovery_Cities : TemplateLib
    {
        public Templates_Discovery_Cities()
        {
            DiscoveryTemplates = new List<MissionTemplate>();
            InvestigationTemplates = new List<MissionTemplate>();
            ShowdownTemplates = new List<MissionTemplate>();

            // ------------------------------- DISCOVERY ------------------------------------------

            // ----- Generic cities (Templates_Cities) -----

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Waggoner Farm",
                Description = "A contact in Waggoner Farm has information on the target",
                Location = "Waggoner Farm",
                formid = FormKeyLookup.GetFormKey("duout_discovery_city_conversation"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "waggonerfarm" }, { "FormId", 0x002CC1EF } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - New Homestead",
                Description = "A contact in New Homestead has information on the target",
                Location = "New Homestead",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "newhomestead" }, { "FormId", 0x0021702B } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Gagarin Landing",
                Description = "A contact in Gagarin Landing has information on the target",
                Location = "Gagarin Landing",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "gagarinlanding" }, { "FormId", 0x00265018 } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - New Atlantis",
                Description = "A contact in New Atlantis has information on the target",
                Location = "New Atlantis",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "newatlantis" }, { "FormId", 0x0001295A } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - The Well",
                Description = "A contact in The Well has information on the target",
                Location = "The Well",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "thewell" }, { "FormId", 0x0019A5C2 } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - HopeTown",
                Description = "A contact in HopeTown has information on the target",
                Location = "HopeTown",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "hopetown" }, { "FormId", 0x00016027 } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Red Mile",
                Description = "A contact at the Red Mile has information on the target",
                Location = "Red Mile",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "redmile" }, { "FormId", 0x002CE0C9 } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Red Mile Crater",
                Description = "A contact at the Red Mile Crater has information on the target",
                Location = "Red Mile Crater",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>()
                {
                    { "ExtraLore", "The Red Mile Run is a dangerous wilderness survival challenge and blood sport held at the Red Mile outpost on Porrima III, where contestants must sprint through a predator-infested valley to activate a distant beacon and return alive. Overseen by proprietor Mei Devine, the event has become a notorious attraction, known for the high-stakes bets spectators place on its runners' survival and the extreme danger posed by the local Red Mile Mauler predators." },
                    { "NeedSpacesuit", false }, { "Label", "redmilecrater" }, { "FormId", 0x002CE0C9 }
                },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - The Den",
                Description = "A contact at The Den has information on the target",
                Location = "The Den",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "spacestation", "conversation" },
                parameters = new Dictionary<string, object>()
                {
                    { "ExtraLore", "The Den is a United Colonies star station in the Wolf system, formerly used as a UC Vanguard outpost after the Colony War." },
                    { "NeedSpacesuit", false }, { "Label", "theden" }, { "FormId", 0x002A0EF4 }
                },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - The Clinic",
                Description = "A contact at The Clinic has information on the target",
                Location = "The Clinic",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "spacestation", "conversation" },
                parameters = new Dictionary<string, object>()
                {
                    { "ExtraLore", "The Clinic is a renowned medical star station in orbit around Deepala in Narion, now run by the Freestar Collective and known as the Settled Systems' leading facility for alien disease research." },
                    { "NeedSpacesuit", false }, { "Label", "theclinic" }, { "FormId", 0x001DE8C0 }
                },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Paradiso",
                Description = "A contact at Paradiso has information on the target",
                Location = "Paradiso",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>()
                {
                    { "ExtraLore", "Paradiso is a luxury beach resort on Porrima II, run by a cutthroat corporate board outside the jurisdiction of the UC or Freestar Collective." },
                    { "NeedSpacesuit", false }, { "Label", "paradiso" }, { "FormId", 0x0026310D }
                },
                Addons = new List<string>(),
            });

            // ----- Neon (Templates_Cities_Neon) -----

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Neon Core",
                Description = "A contact in Neon Core has information on the target",
                Location = "Neon Core",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "neoncore" }, { "FormId", 0x00015FFE } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Neon Ryujin HQ",
                Description = "A contact in Neon Ryujin HQ has information on the target",
                Location = "Neon Ryujin HQ",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "neonryujin" }, { "FormId", 0x00015FFE } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Neon Ebbside",
                Description = "A contact in Neon Ebbside has information on the target",
                Location = "Neon Ebbside",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "neonebbside" }, { "FormId", 0x00015FFE } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Neon Rooftops",
                Description = "A contact in Neon Rooftops has information on the target",
                Location = "Neon Rooftops",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "neonrooftops" }, { "FormId", 0x00015FFE } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Neon Underbelly",
                Description = "A contact in Neon Underbelly has information on the target",
                Location = "Neon Underbelly",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "neonunderbelly" }, { "FormId", 0x00015FFE } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Neon Starport",
                Description = "A contact in Neon Starport has information on the target",
                Location = "Neon Starport",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "neonstarport" }, { "FormId", 0x00015FFE } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Neon Astral Lounge",
                Description = "A contact in Neon Astral Lounge has information on the target",
                Location = "Neon Astral Lounge",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "neonastrallounge" }, { "FormId", 0x00015FFE } },
                Addons = new List<string>(),
            });

            // ----- Akila (Templates_Cities_Akila) -----

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Akila City Farms",
                Description = "A contact in Akila City Farms has information on the target",
                Location = "Akila City Farms",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "akilafarms" }, { "FormId", 0x00010DFB } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Akila City Walls",
                Description = "A contact near the Akila City Walls has information on the target",
                Location = "Akila City Walls",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "akilawalls" }, { "FormId", 0x00010DFB } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Akila City Coe Plaza",
                Description = "A contact in Akila City Coe Plaza has information on the target",
                Location = "Akila City Coe Plaza",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "akilacoeplaza" }, { "FormId", 0x00010DFB } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Akila City The Stretch",
                Description = "A contact in Akila City The Stretch has information on the target",
                Location = "Akila City The Stretch",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "akilathestretch" }, { "FormId", 0x00010DFB } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Akila City The Core",
                Description = "A contact in Akila City The Core has information on the target",
                Location = "Akila City The Core",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "akilathecore" }, { "FormId", 0x00010DFB } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Akila City Midtown",
                Description = "A contact in Akila City Midtown has information on the target",
                Location = "Akila City Midtown",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "akilamidtown" }, { "FormId", 0x00010DFB } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Akila City Spaceport",
                Description = "A contact in Akila City Spaceport has information on the target",
                Location = "Akila City Spaceport",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", false }, { "Label", "akilaspaceport" }, { "FormId", 0x00010DFB } },
                Addons = new List<string>(),
            });

            // ----- Cydonia (Templates_Cities_Cydonia) -----

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Outskirts of Cydonia",
                Description = "A contact at the Outskirts of Cydonia has information on the target",
                Location = "Outskirts of Cydonia",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", true }, { "Label", "cydoniaoutskirts" }, { "FormId", 0x00015FF7 } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Cydonia Central Hub",
                Description = "A contact in the Cydonia Central Hub has information on the target",
                Location = "Cydonia Central Hub",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", true }, { "Label", "cydoniacentralhub" }, { "FormId", 0x00015FF7 } },
                Addons = new List<string>(),
            });

            DiscoveryTemplates.Add(new MissionTemplate()
            {
                Name = "City Contact - Cydonia Residential Level",
                Description = "A contact on the Cydonia Residential Level has information on the target",
                Location = "Cydonia Residential Level",
                formid = FormKeyLookup.GetFormKey("duout_info_planet_conversation_small"),
                outlawQuest = new Discovery_CityConversation(),
                MissionTags = new List<string>() { "discovery", "city", "planetside", "conversation" },
                parameters = new Dictionary<string, object>() { { "NeedSpacesuit", true }, { "Label", "cydoniaresidential" }, { "FormId", 0x00015FF7 } },
                Addons = new List<string>(),
            });
        }
    }
}
