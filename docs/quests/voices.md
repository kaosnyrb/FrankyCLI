# Voices: which ElevenLabs voice to give a speaker

**For whoever writes a Delve's spoken lines** (`speaker` in a recipe, see
[delve-recipes.md](delve-recipes.md) § *Speech*). Pick the voice here, put its id in `speaker.elevenlabs`,
and pick a `speaker.voice` folder that matches it.

*Started 2026-10-09 on his ask: "a file describing the elevenlabs voices so Jessica can choose the right
ones."* Every description says whose it is. **A voice nobody has heard in game is marked so**, because a
label is not a listen.

## The voices

| id | described as | by whom | heard in game | voice type folder | used by |
|---|---|---|---|---|---|
| `CwhRBWXzGAHq8TQ4Fs17` | **A professional-sounding male voice, fairly neutral.** (ElevenLabs labels it *Roger, laid-back, casual, resonant*; his ear wins) | him, 2026-10-09 | ✅ yes, Relay Control | `GenericMale01` | `duo_delve09` (Relay Control) |
| `ClKfJnuqp0hQ7Ax41F4w` | Female. *Ada*: ElevenLabs labels her young, Canadian, casual | the library's labels | ⏳ not in a Delve | `GenericFemale01` | `gen_dlgtest` (a structure test) |
| `21m00Tcm4TlvDq8ikWAM` | The default in `Retrograde.Library/Core/AI/elevenlabsAPI.cs` when no id is given. **Not in his library** (below) | the code | ⏳ no | | nothing on purpose |

**A Delve should always name its voice.** The default exists for a call that forgot; a recipe never relies
on it, and the lint requires `speaker.elevenlabs`.

## Matching the folder

`speaker.voice` is a vanilla **VoiceType**, and it decides only which folder the audio is filed under
(`Data/Sound/Voice/du_overtime.esm/<voice type>/`). The sound is the ElevenLabs voice either way. Keep it
honest anyway: a male voice in `GenericMale01`, a female one in `GenericFemale01`, as his Outlaws 02 ships.

## Adding a voice

When he tries a new one, add a row: the id, **his** words for how it sounds, and whether it has been heard
in game. Not the voice's name from the ElevenLabs site, and not a guess from the name: the library's labels
are below, and a label is what ElevenLabs says, not what it sounds like in Starfield.

## His library: every voice on the account, with ElevenLabs' own labels

*Read off the API on 2026-10-09 (`GET /v1/voices`; he granted the key `voices_read` that afternoon), 51 voices,
sorted by gender then age. **These are ElevenLabs' labels, not anyone's listen.** Names are theirs, with dashes
normalised. ⚠ A snapshot: the account can gain or lose voices, so re-read it before trusting that an id is still
there. The heard-in-game table above is the one to choose from first; this is where to look for a new one.*

| gender | age | name (ElevenLabs') | id | accent | made for | tone | kind |
|---|---|---|---|---|---|---|---|
| female | young | Ada | `ClKfJnuqp0hQ7Ax41F4w` | canadian | social media | casual | professional |
| female | young | Ava - Neutral Conversational British | `wCizWKvnaS3LOIUwHo98` | british | conversational | neutral | professional |
| female | young | Cat - Droll and Dry | `54Cze5LrTSyLgbO6Fhlc` | american | characters animation | sassy | professional |
| female | young | Clara - Direct, Firm and Confident | `OFIZL27ncTeMt1qVKEzH` | american | conversational | casual | professional |
| female | young | Influencer Emily | `odyUrTN5HMVKujvVAgWW` | irish | social media | casual | professional |
| female | young | Jessica - Playful, Bright, Warm | `cgSgspJ2msm6clMCkdW9` | american | conversational | cute | premade |
| female | young | Laura - Enthusiast, Quirky Attitude | `FGY2WhTYpPnrIDTdsKH5` | american | social media | sassy | premade |
| female | young | Lucy - Fresh & Casual | `lcMyyd2HUfFzxdCaC4Ta` | british | conversational | casual | professional |
| female | young | Lyan - Conversational Female Character | `tnVKC6NjwhdRxoQIfKue` | american | characters animation | casual | professional |
| female | young | Megan | `E393dkE75hqtz1LO2aEJ` | irish | social media | confident | professional |
| female | young | Sara -New England Story Teller Girl | `BXeZVLBk6Y5kMHj9cDEY` | american | conversational | cute | professional |
| female | young | Sarah - Mature, Reassuring, Confident | `EXAVITQu4vr4xnSDxMaL` | american | entertainment tv | professional | premade |
| female | young | Serafina - Flirty Sensual Temptress | `4tRn1lSkEn13EVTuqb0g` | american | characters animation | mature | professional |
| female | young | Shelby | `rfkTsdZrVWEVhDycUYn9` | british | conversational | pleasant | professional |
| female | young | Verity - Chatty, Fast-Paced, Fun Storyteller | `oW8bn5YtBB89X2nJ0DT9` | british | conversational | upbeat | professional |
| female | middle aged | Alice - Clear, Engaging Educator | `Xb7hH8MSUJpSbSDYk0k2` | british | informative educational | professional | premade |
| female | middle aged | Annie K - Calm, Grounded Narrator | `XW70ikSsadUbinwLMZ5w` | american | narrative story | professional | professional |
| female | middle aged | Bella - Professional, Bright, Warm | `hpp4J3VqNfWAUOO0d1Us` | american | informative educational | professional | premade |
| female | middle aged | Emma | `56bWURjYFHyYyVf490Dp` | australian | conversational | neutral | professional |
| female | middle aged | Isla | `h8eW5xfRUGVJrZhAFxqK` | scottish | conversational | chill | professional |
| female | middle aged | Jessica Gallagher | `DbwWo4rVEd5NrejHYUnm` | irish | informative educational | confident | professional |
| female | middle aged | Katie X - Clear British Customer Support | `MzqUf1HbJ8UmQ0wUsx2p` | british | conversational | professional | professional |
| female | middle aged | Laura - a top narration voice | `GZ4PpFJV8ikEGUtBrjK7` | american | narrative story | sassy | professional |
| female | middle aged | Lauren B - Warm, Humanlike, Friendly Conversational, Chatbots | `l4Coq6695JDX9xtLqXDE` | american | conversational | pleasant | professional |
| female | middle aged | Lily - Velvety Actress | `pFZP5JQG7iQjIQuC4Bku` | british | informative educational | confident | premade |
| female | middle aged | Lucy - Friendly, Relaxed, Thoughtful | `r5iFzIytiA1rzjhWFCjW` | american | conversational | pleasant | professional |
| female | middle aged | Matilda - Knowledgable, Professional | `XrExE9yKIg1WjnnlVkGX` | american | informative educational | upbeat | premade |
| female | old | Jeanette - Audiobook (2025-02-08_1330) | `RILOU7YmBhvwJGDGjNmP` | british | narrative story | professional | professional |
| male | young | Charlie - Deep, Confident, Energetic | `IKne3meq5aSn9XLyUdCD` | australian | conversational | hyped | premade |
| male | young | Harry - Fierce Warrior | `SOYHLrjzK2X1ezoPC6cr` | american | characters animation | rough | premade |
| male | young | Hunter - YouTube Narrator | `inKOuEy40NdNVHxcqekZ` | canadian | social media | chill | professional |
| male | young | Killian | `CsbTapRVtZdcBs3vBbQe` | irish | narrative story | calm | professional |
| male | young | Liam - Energetic, Social Media Creator | `TX3LPaxmHKxFdv7VOQHJ` | american | social media | confident | premade |
| male | young | Royce - Suspense, Mystery & Thrill Narration | `omRordDZNt4Gy45cetUa` | australian | narrative story | confident | professional |
| male | young | Will - Relaxed Optimist | `bIHbv24MWmeRgasZH58o` | american | conversational | chill | premade |
| male | middle aged | Adam - Dominant, Firm | `pNInz6obpgDQGcFmaJgB` | american | social media |  | premade |
| male | middle aged | Brian - Deep, Resonant and Comforting | `nPczCjzI2devNBz1zQrb` | american | social media | classy | premade |
| male | middle aged | Callum - Husky Trickster | `N2lVS1w4EtoT3dr4eOWO` | american | characters animation |  | premade |
| male | middle aged | Chris - Charming, Down-to-Earth | `iP95p4xoKVk53GoZ742B` | american | conversational | casual | premade |
| male | middle aged | Daniel - Steady Broadcaster | `onwK4e9ZLuTAKqWW03F9` | british | informative educational | formal | premade |
| male | middle aged | David Jarrett | `CjIKi1zuI666pVsFrtyU` | australian | narrative story | casual | professional |
| male | middle aged | Eric - Smooth, Trustworthy | `cjVigY5qzO86Huf0OWal` | american | conversational | classy | premade |
| male | middle aged | George - Warm, Captivating Storyteller | `JBFqnCBsd6RMkjVDRZzb` | british | narrative story | mature | premade |
| male | middle aged | Hector | `dgkKQcJqyy5AP0dqleUU` | scottish | narrative story | deep | professional |
| male | middle aged | Jon Jebus | `wsHauqjSkdBeAvdbUFmR` | american | conversational | casual | professional |
| male | middle aged | Julian - Male Australian Accent | `7QTeMqXOsYj1AQaKLcrf` | australian | informative educational | confident | professional |
| male | middle aged | Roger - Laid-Back, Casual, Resonant | `CwhRBWXzGAHq8TQ4Fs17` | american | conversational | classy | premade |
| male | middle aged | Simon - the voice of online training | `zcIk2xc7SGwlywr4TzZu` | australian | informative educational | professional | professional |
| male | old | Bill - Wise, Mature, Balanced | `pqHfZKP75CvOlQylNhV4` | american | advertisement | crisp | premade |
| neutral | young | Xiaoxi - Chinese American podcast host | `rk9BD4xwuG39syvDIBQy` | american | conversational | confident | professional |
| neutral | middle aged | River - Relaxed, Neutral, Informative | `SAz9YHcvj6GT2YYXdXww` | american | conversational | calm | premade |

## What changing a voice costs

Changing `speaker.elevenlabs` (or the words) regenerates those lines at the next build. One call each,
priced in characters. Unchanged lines come from the voice cache (`C:/modding/DU_Overtime/voicecache/`), which
keys each line on voice, voice type and exact words.
