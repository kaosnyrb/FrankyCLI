using Retrograde;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public class FlavourSeedData
    {
        public static string GetNpcConversationTone()
        {
            List<string> tones = new()
            {
                "This character is clearly annoyed about something unrelated — they keep getting pulled back to it mid-sentence.",
                "This character's mind is elsewhere; they're physically present but only half listening, giving short distracted answers.",
                "This character is in a rush and keeps glancing around like they're already late for something.",
                "This character is tired — not emotional, just bone-tired — and it colours everything they say with a low-level irritability.",
                "This character is in an unusually good mood about something and it makes them slightly too chatty and loose-lipped.",
                "This character is nursing a grievance with someone nearby and keeps making pointed comments that don't quite land for the player.",
                "This character is hungover and doing their best to hold it together, wincing at loud sounds and keeping answers short.",
                "This character is nervous about something happening later and their focus keeps slipping toward it.",
                "This character is bored out of their mind and treats this conversation as the most interesting thing that's happened all shift.",
                "This character is clearly watching someone else across the room and only intermittently paying attention.",
                "This character is coming down from something and is slower than usual, pausing too long between thoughts.",
                "This character has clearly just had an argument and the residual tension bleeds into how they respond.",
                "This character is trying to eat or drink while talking and keeps getting interrupted by their own mouthfuls.",
                "This character is in the middle of a task they haven't put down, responding while their hands are still busy.",
                "This character is putting on a performance of friendliness that doesn't quite reach their eyes — something is off.",
            };

            return tones[RandomProvider.Random.Next(tones.Count)];
        }

        public static string GetNpcRelationshipToTarget()
        {
            List<string> relationships = new()
            {
                "This character met the target briefly in passing — they shared a docking bay queue and the target said something that stuck.",
                "This character and the target were drinking buddies at a bar — they know the target's habits and usual haunts but nothing deeper.",
                "This character used to work alongside the target in a previous job and they drifted apart when one of them moved on.",
                "This character crossed paths with the target enough times in the starport to know their name and face, but not much else.",
                "This character and the target are old colleagues who worked the same shift or route — they haven't spoken in a while but there's mutual respect.",
                "This character once covered for the target in a tight spot and feels they're owed something — they're reluctant but not unwilling to talk.",
                "This character was a regular at the same late-night spot as the target and knows their patterns and the people they used to meet.",
                "This character shared a bunkroom or cheap lodgings with the target for a few weeks on a job — enough to pick up small details.",
                "This character used to run into the target at the same supply depot on a regular basis — they'd chat but never socialised outside that.",
                "This character and the target had a mutual contact who introduced them once; they never got close but remember the target clearly.",
                "This character played cards with the target a handful of times and knows how they think under pressure.",
                "This character gave the target a lift on a transport run and they talked the whole way — one of those conversations that goes surprisingly deep.",
                "This character worked a job adjacent to the target's and they'd occasionally eat at the same canteen, nodding acquaintances at best.",
                "This character was a regular at the same repair shop or supplier as the target — they'd wait together sometimes and swap small talk.",
                "This character and the target briefly shared a crew on a short contract — not long enough to be friends, but long enough to read each other.",
            };

            return relationships[RandomProvider.Random.Next(relationships.Count)];
        }
    }
}
