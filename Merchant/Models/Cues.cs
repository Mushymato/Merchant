using StardewValley;
using StardewValley.GameData;

namespace Merchant.Models;

public enum CueType
{
    // vanilla cues
    trackGameplay = 0,
    trackReport = 1,
    hagglePing = 2,
    haggleSlide = 3,
    haggleNegotiate = 4,
    haggleSuccess = 5,
    haggleFailure = 6,

    // non-vanilla cues
    doorbell = 7,
}

public static class Cues
{
    internal const string DoorbellCue = $"{ModEntry.ModId}_doorbell";

    private static readonly List<string> soundCues = Enumerable.Repeat(string.Empty, 8).ToList();

    internal static void CueListSetup()
    {
        soundCues[(int)CueType.trackGameplay] = "event2";
        soundCues[(int)CueType.trackReport] = "harveys_theme_jazz";
        soundCues[(int)CueType.hagglePing] = "junimoKart_coin";
        soundCues[(int)CueType.haggleSlide] = "flute";
        soundCues[(int)CueType.haggleNegotiate] = "smallSelect";
        soundCues[(int)CueType.haggleSuccess] = "reward";
        soundCues[(int)CueType.haggleFailure] = "fishEscape";
        soundCues[(int)CueType.doorbell] = DoorbellCue;

        for (int i = 0; i < soundCues.Count; i++)
        {
            string overrideCue = $"{ModEntry.ModId}_{(CueType)i}";
            if (Game1.CueModification.cueModificationData.ContainsKey(overrideCue))
            {
                ModEntry.Log($"{(CueType)i} mapped to '{overrideCue}'");
                soundCues[i] = overrideCue;
            }
            else
            {
                ModEntry.Log($"{(CueType)i} default '{soundCues[i]}'");
            }
        }
    }

    internal static void PlaySound(CueType cue) => Game1.playSound(soundCues[(int)cue]);

    internal static void PlaySound(CueType cue, int pitch) => Game1.playSound(soundCues[(int)cue], pitch);

    internal static void PlaySound(CueType cue, int pitch, out ICue icue) =>
        Game1.playSound(soundCues[(int)cue], pitch, out icue);

    internal static void PlayMusic(CueType cue)
    {
        Game1.stopMusicTrack(MusicContext.MiniGame);
        Game1.changeMusicTrack(soundCues[(int)cue], false, MusicContext.MiniGame);
    }
}
