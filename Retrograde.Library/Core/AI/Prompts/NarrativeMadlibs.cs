using Retrograde.Nouns;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class NarrativeMadlibs
    {
        // ------------------------------
        // First Person Account
        // (replaces NarrativePrompts.GetFirstPersonAccount)
        // ------------------------------
        public static string GetFirstPersonAccount(OutlawNpc outlaw, string currentLocation, string nextLocation, string contextHint = "")
        {
            string speaker = NarrativeSeedData.SpeakerTypes[RandomProvider.Random.Next(NarrativeSeedData.SpeakerTypes.Count)];
            string name    = outlaw.name;
            string crime   = outlaw.Traits.Crime;
            string occ     = outlaw.Traits.Occupation;
            string hint    = string.IsNullOrWhiteSpace(contextHint) ? currentLocation : contextHint;

            var templates = new List<string>
            {
                $"I knew {name} from {currentLocation}. The {crime} — I heard about it later. Didn't suspect a thing at the time. They just up and left one day without a word. Someone mentioned {nextLocation} was where they were heading. I don't know what you'll find there, but that's the lead I've got.",
                $"Saw {name} leaving {currentLocation} in a hurry. Wouldn't look at me. I'd heard talk about the {crime} and kept my head down. If they're still out there, {nextLocation} came up more than once in conversation before they left. That's all I know.",
                $"I found this when {name} cleared out of {hint}. Former {occ}, but that clearly changed. The {crime} business got around. Whoever's looking — {nextLocation} is the name they kept mentioning. Make of that what you will.",
                $"They used to come through {currentLocation} regular. Former {occ}, good at keeping quiet. But people talk. The {crime} story got around. Heard they moved toward {nextLocation}. Haven't seen them since and I'm not looking. This is all I've got for you.",
                $"I reported {name} once. Didn't go anywhere. The {crime} — there were signs. I just didn't know what I was seeing. Last I heard they were out of {currentLocation} and heading for {nextLocation}. I stopped asking after that."
            };

            return templates[RandomProvider.Random.Next(templates.Count)];
        }

        // ------------------------------
        // Derelict Ship Transmission
        // (replaces NarrativePrompts.GetTransmission)
        // ------------------------------
        public static string GetTransmission(OutlawNpc outlaw, string currentLocation, string nextLocation, string contextHint = "")
        {
            string transmissionType = NarrativeSeedData.TransmissionTypes[RandomProvider.Random.Next(NarrativeSeedData.TransmissionTypes.Count)];
            string name  = outlaw.name;
            string crime = outlaw.Traits.Crime;
            string ship  = string.IsNullOrWhiteSpace(contextHint) ? currentLocation : contextHint;

            var templates = new List<string>
            {
                $"This is {transmissionType}. Ship: {ship}. Crew status unknown. Whatever happened here — it traces back to {name}. {crime}. If you're hearing this, the trail doesn't end on this ship. The name you need, the place you need — {nextLocation}. Go.",
                $"Found on {ship}. Timestamp corrupted. Someone left this deliberately. Last entry in the navigation log references {name} and a course change toward {nextLocation}. This was not an accident. Follow the name.",
                $"Recovery log — {ship}. I don't know who'll find this. What happened here connects to {name} — {crime}. If you want the full story, {nextLocation} is where you need to be. I'm not going to make it there. Finish it.",
                $"Intercepted signal, stored to local memory — {ship}. Crew silent for hours. Last confirmed destination: {nextLocation}. Personal logs reference {name} repeatedly. This was deliberate. Someone knew. Follow the trail.",
                $"This is {transmissionType} from {ship}. Systems failing. The information you need is at {nextLocation}. {name} — {crime} — that's who you're looking for. There's nothing left here worth staying for."
            };

            return templates[RandomProvider.Random.Next(templates.Count)];
        }

        // ------------------------------
        // Outlaw Personal Log
        // (replaces NarrativePrompts.GetOutlawLogfile — same signature)
        // ------------------------------
        public static string GetOutlawLogfile(string name, string gender, OutlawTraits traits)
        {
            string focus = traits.CurrentPreoccupation;
            string crime = traits.Crime;
            string occ   = traits.Occupation;

            var templates = new List<string>
            {
                $"I keep thinking about {focus}. Didn't expect it to matter this much. Everything I did — the {crime}, all of it — felt necessary at the time. Maybe it was. I don't know anymore.\n\nI was {occ} once. That feels like a different life. Someone else's story.\n\nIf you're listening to this... you probably know how it ends. I just wanted someone to know it was more complicated than that. It always is.",

                $"Whatever you think you know about me, you're wrong. The {crime} — there was a reason. There's always a reason.\n\nI was {occ}. I was good at it. Then things changed and I had to change too. That's all this is.\n\nI've been thinking about {focus}. That's what I keep coming back to. Not regret. Never that. Just... unfinished business. [exhales sharply]",

                $"I know what this sounds like. Another fugitive trying to explain themselves into a better story.\n\nI'm not doing that. The {crime} happened. I was {occ} and then I wasn't and then... this.\n\nThe thing that stays with me — {focus}. Strange what matters at the end.\n\n[sighs]\n\nI'm not asking for anything. I just wanted to say it out loud, once.",

                $"They're getting closer. I can feel it.\n\nI've been going over it — the {crime}, every decision, every moment I could have stopped. I was {occ}. I thought I understood how this worked.\n\nKept coming back to {focus}. Couldn't let it go. Maybe that's what got me in the end.\n\n[whispers] Don't trust anyone. If you found this... then you already know.",

                $"Documenting this for the record. {crime} — that's what they'll call it. I have a different word for it.\n\nFormer {occ}. I know how evidence works. I know how this gets used.\n\nWhat I keep returning to: {focus}. I made a choice. I'd probably make the same one again.\n\n[exhales sharply] That's all there is to say.",

                $"I used to think about what I'd say at the end. Turns out you don't have the words when it comes to it.\n\nI was {occ}. That part of my life feels very far away now. The {crime} — I know how it looks.\n\n{focus}. That's what I've been carrying.\n\n[sighs] I hope whoever finds this understands it was never simple. None of it was ever simple."
            };

            return templates[RandomProvider.Random.Next(templates.Count)];
        }

        // ------------------------------
        // Mission Briefing Dataslate
        // (replaces NarrativePrompts.GetMissionBriefingDataslate)
        // ------------------------------
        public static string GetMissionBriefingDataslate(OutlawNpc outlaw, string firstLocation)
        {
            string name  = outlaw.name;
            string crime = outlaw.Traits.Crime;
            string occ   = outlaw.Traits.Occupation;

            var templates = new List<string>
            {
                $"Target: {name}. Former {occ}. Wanted for {crime}. No registered address. Confirmed using aliases. Start at {firstLocation} — last reliable sighting places them in that area. Find evidence connecting them to their associates. Move quick. Another crew is already working this bounty.",
                $"Bounty active. Subject: {name}. {occ}, now fugitive. Crime: {crime}. Last known: {firstLocation}. Believed to be operating through local contacts. Approach with standard precautions — subject is experienced and unlikely to cooperate. Window closes soon.",
                $"{name}. {crime}. Former {occ} — knows how to disappear. Start at {firstLocation}. Evidence of their operation should still be there. Pick up the trail before it goes cold. Rival crew is already sniffing around the same lead. Do not let them get there first."
            };

            return templates[RandomProvider.Random.Next(templates.Count)];
        }
    }
}
