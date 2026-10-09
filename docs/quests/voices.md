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
| `CwhRBWXzGAHq8TQ4Fs17` | **A professional-sounding male voice, fairly neutral.** | him, 2026-10-09 | ✅ yes, Relay Control | `GenericMale01` | `duo_delve09` (Relay Control) |
| `ClKfJnuqp0hQ7Ax41F4w` | Female. Labelled *"Ada (female)"* in `commands/quest/gen_dlgtest.cs` | the code's comment | ⏳ not in a Delve | `GenericFemale01` | `gen_dlgtest` (a structure test) |
| `21m00Tcm4TlvDq8ikWAM` | The default in `Retrograde.Library/Core/AI/elevenlabsAPI.cs` when no id is given | the code | ⏳ no | | nothing on purpose |

**A Delve should always name its voice.** The default exists for a call that forgot; a recipe never relies
on it, and the lint requires `speaker.elevenlabs`.

## Matching the folder

`speaker.voice` is a vanilla **VoiceType**, and it decides only which folder the audio is filed under
(`Data/Sound/Voice/du_overtime.esm/<voice type>/`). The sound is the ElevenLabs voice either way. Keep it
honest anyway: a male voice in `GenericMale01`, a female one in `GenericFemale01`, as his Outlaws 02 ships.

## Adding a voice

When he tries a new one, add a row: the id, **his** words for how it sounds, and whether it has been heard
in game. Not the voice's name from the ElevenLabs site, and not a guess from the name. ⚠ The build's API key
can generate speech but cannot list the account's voices (`voices_read` is not granted), so this table is
the only list there is; it does not fill itself.

## What changing a voice costs

Changing `speaker.elevenlabs` (or the words) regenerates those lines at the next build. One call each,
priced in characters. Unchanged lines come from the voice cache (`C:/modding/DU_Overtime/voicecache/`), which
keys each line on voice, voice type and exact words.
