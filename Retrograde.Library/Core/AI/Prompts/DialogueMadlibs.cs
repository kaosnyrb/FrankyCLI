using Retrograde.Models;
using Retrograde.Nouns;
using System.Collections.Generic;

namespace Retrograde.AI.Utils
{
    public static class DialogueMadlibs
    {
        /// <summary>
        /// Generates a DialogueScript from madlibs templates — no AI call.
        /// The result is later refined by WritingPolishPass via DialogueScriptPolishable.
        /// </summary>
        public static DialogueScript GetDialogueScript(OutlawNpc outlaw, string npcName, string currentLocation, string nextLocation, string npcBackground = "")
        {
            string name  = outlaw.name;
            string crime = outlaw.Traits.Crime;
            string occ   = outlaw.Traits.Occupation;

            int choice = RandomProvider.Random.Next(5);
            var script = choice switch
            {
                0 => Template_DocksContact(name, occ, crime, currentLocation, nextLocation),
                1 => Template_NervousWitness(name, occ, crime, currentLocation, nextLocation),
                2 => Template_LocalContact(name, occ, crime, currentLocation, nextLocation),
                3 => Template_OldConnection(name, occ, crime, currentLocation, nextLocation),
                _ => Template_ProfessionalSource(name, occ, crime, currentLocation, nextLocation),
            };

            script.NpcBackground = npcBackground;
            return script;
        }

        // Clamp to char limit, truncating cleanly at a word boundary.
        private static string Clamp(string s, int max)
        {
            if (s.Length <= max) return s;
            for (int i = max - 1; i >= 0; i--)
                if (s[i] == ' ') return s.Substring(0, i).TrimEnd();
            return s.Substring(0, max);
        }

        // ── Template 1: Docks contact who saw them pass through ─────────────────
        private static DialogueScript Template_DocksContact(string name, string occ, string crime, string loc, string nextLoc) => new()
        {
            NpcGreeting = Clamp($"You hunting {name}? Figured someone would show up eventually.", 100),
            CompletionDismissal = Clamp("Nothing left to tell you. Move on.", 100),
            Exchanges = new List<DialogueExchange>
            {
                new()
                {
                    PlayerPrompt = Clamp("What do you know about them?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"{name} came through {loc} regular. Former {occ}.", 150),
                        Clamp($"Got into {crime} somewhere along the way. Wasn't subtle about it.", 150),
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Which way did they go?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Left two days back. Took passage on something heading out of here.", 150),
                        Clamp($"Heard {nextLoc} mentioned before they went. That's the best I can give you.", 150),
                    },
                    SideOptions = new DialogueSideOptions
                    {
                        ExtraInfo = new SideOption
                        {
                            PlayerPrompt = Clamp("What ship?", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("Didn't catch the registration. Private vessel, not a liner. Small crew.", 150),
                            }
                        },
                        Joke = new SideOption
                        {
                            PlayerPrompt = Clamp("Very thorough. Thanks.", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("I'm a source, not a manifest clerk.", 150),
                            }
                        }
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Who else is looking?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Someone else came through asking the same questions yesterday.", 150),
                        Clamp($"Move fast. {nextLoc} — that's where the lead goes. Don't waste it.", 150),
                    }
                }
            }
        };

        // ── Template 2: Nervous witness who helped unknowingly ───────────────────
        private static DialogueScript Template_NervousWitness(string name, string occ, string crime, string loc, string nextLoc) => new()
        {
            NpcGreeting = Clamp($"I heard you were looking for {name}. I don't want trouble.", 100),
            CompletionDismissal = Clamp("I've said all I'm going to. Please leave it there.", 100),
            Exchanges = new List<DialogueExchange>
            {
                new()
                {
                    PlayerPrompt = Clamp("Tell me what you saw.", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"They were here in {loc} for weeks. Seemed normal — used to be {occ}.", 150),
                        Clamp($"I didn't know about the {crime} until after they'd gone.", 150),
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("What tipped you off?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"The way they left. Too fast, too clean. People asked questions after.", 150),
                        Clamp($"Someone mentioned {nextLoc}. I don't know if that's where they went or just where they were watching.", 150),
                    },
                    SideOptions = new DialogueSideOptions
                    {
                        ExtraInfo = new SideOption
                        {
                            PlayerPrompt = Clamp("Who was asking questions?", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("Didn't give names. Not local. They left before I could get a better look.", 150),
                            }
                        },
                        Joke = new SideOption
                        {
                            PlayerPrompt = Clamp("You notice a lot for someone not involved.", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("I notice everything. It's how I stay out of trouble. Mostly.", 150),
                            }
                        }
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Where are they now?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"I told you — {nextLoc}. That's the only name I heard.", 150),
                        Clamp("Whatever they're into, I want no part of it. That's all I know.", 150),
                    }
                }
            }
        };

        // ── Template 3: Local who noticed too much ────────────────────────────────
        private static DialogueScript Template_LocalContact(string name, string occ, string crime, string loc, string nextLoc) => new()
        {
            NpcGreeting = Clamp($"You're not the first person to come asking about {name}.", 100),
            CompletionDismissal = Clamp("I've got nothing more for you. Go do what you came to do.", 100),
            Exchanges = new List<DialogueExchange>
            {
                new()
                {
                    PlayerPrompt = Clamp("What can you tell me?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"I knew them by sight. Former {occ} — you'd never guess from looking.", 150),
                        Clamp($"The {crime} — I heard it secondhand. But it made sense once I did.", 150),
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Where did they go from here?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Out of {loc} fast, a few days ago. Didn't look back.", 150),
                        Clamp($"Picked up {nextLoc} from someone at the port. Unconfirmed — but it's the only lead I've got.", 150),
                    },
                    SideOptions = new DialogueSideOptions
                    {
                        ExtraInfo = new SideOption
                        {
                            PlayerPrompt = Clamp("What did they look like leaving?", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("Light kit. Traveling fast, not comfortable. Someone running, not relocating.", 150),
                            }
                        },
                        Joke = new SideOption
                        {
                            PlayerPrompt = Clamp("Unconfirmed. Great.", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("You want certainty, hire a data broker. I'm giving you what I've got.", 150),
                            }
                        }
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Anyone else working this?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Saw someone else asking around yesterday. Didn't recognize them.", 150),
                        Clamp($"{nextLoc} — get there before they do. That's my advice.", 150),
                    }
                }
            }
        };

        // ── Template 4: Old connection, reluctant to talk ────────────────────────
        private static DialogueScript Template_OldConnection(string name, string occ, string crime, string loc, string nextLoc) => new()
        {
            NpcGreeting = Clamp($"I know why you're here. Just... give me a moment.", 100),
            CompletionDismissal = Clamp("We're done. I hope it ends clean.", 100),
            Exchanges = new List<DialogueExchange>
            {
                new()
                {
                    PlayerPrompt = Clamp("Tell me about them.", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"I've known {name} a long time. {occ}, or used to be.", 150),
                        Clamp($"The {crime} — I didn't know the full of it until recently. I should have asked more questions.", 150),
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Where did they go?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"They left {loc} quietly. Didn't say goodbye — that told me enough.", 150),
                        Clamp($"They mentioned {nextLoc} once, a while back. Something they were watching.", 150),
                    },
                    SideOptions = new DialogueSideOptions
                    {
                        ExtraInfo = new SideOption
                        {
                            PlayerPrompt = Clamp("What were they watching there?", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("They never said directly. But they kept coming back to it. That means something.", 150),
                            }
                        },
                        Joke = new SideOption
                        {
                            PlayerPrompt = Clamp("Didn't say goodbye. Heartbreaking.", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("You get to make jokes. I have to live with it. Keep moving.", 150),
                            }
                        }
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Is there anything else?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Just... don't make it worse than it has to be.", 150),
                        Clamp($"{nextLoc}. That's where you'll find them. Do what you came to do.", 150),
                    }
                }
            }
        };

        // ── Template 5: Professional source, trades in information ───────────────
        private static DialogueScript Template_ProfessionalSource(string name, string occ, string crime, string loc, string nextLoc) => new()
        {
            NpcGreeting = Clamp($"Bounty on {name}? I've been tracking that one myself.", 100),
            CompletionDismissal = Clamp("Come back when you have a new contract. Otherwise we're done.", 100),
            Exchanges = new List<DialogueExchange>
            {
                new()
                {
                    PlayerPrompt = Clamp("What do you have?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Former {occ}. Smart, knows how to move. The {crime} was well-planned — almost.", 150),
                        Clamp($"They've been using {loc} as cover. Not for much longer, I'd guess.", 150),
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Where are they headed?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Pulled a departure log two days ago. Course filed toward the {nextLoc} area.", 150),
                        Clamp($"Whether that's where they're going or just what they want logged — your call.", 150),
                    },
                    SideOptions = new DialogueSideOptions
                    {
                        ExtraInfo = new SideOption
                        {
                            PlayerPrompt = Clamp("What name were they using?", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("Three aliases on file. Rotate them. The transit record used the oldest one.", 150),
                            }
                        },
                        Joke = new SideOption
                        {
                            PlayerPrompt = Clamp("Or just where they want logged. Helpful.", 45),
                            NpcReply = new List<string>
                            {
                                Clamp("I give you data. You do the hunting. That's the arrangement.", 150),
                            }
                        }
                    }
                },
                new()
                {
                    PlayerPrompt = Clamp("Anything else I should know?", 45),
                    NpcReply = new List<string>
                    {
                        Clamp($"Another crew picked up the same trail this morning. They're good.", 150),
                        Clamp($"Get to {nextLoc} first. That's worth more than anything else I can tell you.", 150),
                    }
                }
            }
        };
    }
}
