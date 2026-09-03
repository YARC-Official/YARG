using UnityEngine;
using YARG.Core;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Logging;
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

        private static string GetInstrumentSprite(Instrument instrument, int harmonyIndex, bool isMissingDevice)
        {
            if (isMissingDevice)
            {
                return $"NoInstrumentIcons[{instrument.ToResourceName()}]";
            }

            if (instrument == Instrument.Harmony)
            {
                return $"HarmonyVocalsIcons[{harmonyIndex + 1}]";
            }

            return $"InstrumentIcons[{instrument.ToResourceName()}]";
        }

        public static Color GetHarmonyColor(this YargPlayer player)
        {
            return GetHarmonyColor(player.Profile.CurrentInstrument, player.Profile.HarmonyIndex,
                player.IsMissingInputDevice || player.IsMissingMicrophone, player.SittingOut,
                player.ColorProfile);
        }

        private static Color GetHarmonyColor(Instrument instrument, int harmonyIndex, bool isMissingDevice,
            bool isSittingOut, ColorProfile colorProfile)
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
            if (instrument != Instrument.Harmony)
            {
                return Color.white;
            }

            if (harmonyIndex < 0 || harmonyIndex > 2)
            {
                YargLogger.LogWarning("PlayerNameDisplay", $"Harmony index {harmonyIndex} is out of bounds.");
                return Color.white;
            }

            return colorProfile.Vocals.GetPartColor(harmonyIndex, isHarmony: true).ToUnityColor();
        }
    }
}
