using UnityEngine;
using YARG.Core;
using YARG.Core.Engine;
using YARG.Core.Engine.Vocals.Engines;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Gameplay.HUD
{
    public static class InstrumentIconProvider
    {
        public static string GetInstrumentSprite(this EngineManager.EngineContainer container)
        {
            return GetInstrumentSprite(container.Instrument, container.HarmonyIndex, false);
        }

        public static string GetInstrumentSprite(this YargPlayer player)
        {
            var isMissingDevice = player.IsMissingInputDevice || player.IsMissingMicrophone;
            return GetInstrumentSprite(player.Profile.CurrentInstrument, player.Profile.HarmonyIndex, isMissingDevice);
        }

        // A party vocals container is built on the harmony chart's parts, so its
        // instrument reports as Harmony; identify it by its coordinator engine instead.
        private static bool IsPartyVocals(this EngineManager.EngineContainer container)
        {
            return container.BaseEngine is PartyVocalsCoordinatorEngine;
        }

        // Song-aware overloads for gameplay HUDs (player banner, fail meter):
        // party vocals is part-agnostic, so its icon reflects the song's vocal
        // part count instead of the player's harmony index.
        public static string GetInstrumentSprite(this EngineManager.EngineContainer container, SongEntry song)
        {
            if (song != null && container.IsPartyVocals())
            {
                return GetPartyVocalsSprite(song.VocalsCount);
            }

            return GetInstrumentSprite(container);
        }

        public static string GetInstrumentSprite(this YargPlayer player, SongEntry song)
        {
            if (song != null && player.Profile.CurrentInstrument == Instrument.PartyVocals)
            {
                return GetPartyVocalsSprite(song.VocalsCount);
            }

            return GetInstrumentSprite(player);
        }

        private static string GetInstrumentSprite(Instrument instrument, int harmonyIndex, bool isMissingDevice)
        {
            if (isMissingDevice)
            {
                return $"NoInstrumentIcons[{instrument.ToResourceName()}]";
            }

            if (instrument == Instrument.Harmony || instrument == Instrument.PartyVocals)
            {
                return $"HarmonyVocalsIcons[{harmonyIndex + 1}]";
            }

            return $"InstrumentIcons[{instrument.ToResourceName()}]";
        }

        private static string GetPartyVocalsSprite(int vocalsCount)
        {
            // Part-count based icon: solo mic, two mics, or harmony mics
            return vocalsCount switch
            {
                >= 3 => "InstrumentIcons[harmVocals]",
                2 => "InstrumentIcons[twoVocals]",
                _ => "InstrumentIcons[vocals]",
            };
        }

        public static Color GetHarmonyColor(this YargPlayer player)
        {
            return GetHarmonyColor(player.Profile.CurrentInstrument, player.Profile.HarmonyIndex, player.IsMissingInputDevice || player.IsMissingMicrophone, player.SittingOut);
        }

        public static Color GetHarmonyColor(this EngineManager.EngineContainer container)
        {
            return GetHarmonyColor(container.Instrument, container.HarmonyIndex, false, false);
        }

        // Color for gameplay HUD icons: party vocals is part-agnostic, so it stays untinted
        public static Color GetGameplayIconColor(this YargPlayer player)
        {
            if (player.Profile.CurrentInstrument == Instrument.PartyVocals)
            {
                return Color.white;
            }

            return GetHarmonyColor(player);
        }

        public static Color GetGameplayIconColor(this EngineManager.EngineContainer container)
        {
            if (container.IsPartyVocals())
            {
                return Color.white;
            }

            return GetHarmonyColor(container);
        }

        private static Color GetHarmonyColor(Instrument instrument, int harmonyIndex, bool isMissingDevice, bool isSittingOut)
        {
            if (isMissingDevice)
            {
                // NoInstrumentIcons are coloured to begin with - don't override that.
                return Color.white;
            }
            if (isSittingOut)
            {
                return Color.gray;
            }
            if (instrument != Instrument.Harmony && instrument != Instrument.PartyVocals)
            {
                return Color.white;
            }

            if (harmonyIndex >= VocalTrack.Colors.Length)
            {
                YargLogger.LogWarning("PlayerNameDisplay", $"Harmony index {harmonyIndex} is out of bounds.");
                return Color.white;
            }

            return VocalTrack.Colors[harmonyIndex];
        }
    }
}
