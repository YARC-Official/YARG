using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public enum BindingType
    {
        Button,
        IndividualButton,
        DrumButton,
        Axis,
        Integer
    }

    public struct InputActionInfo
    {
        public BindingType Type { get; }
        public int Action { get; }
        public string LocalizationKey { get; }
        public string LeftyLocalizationKey { get; }

        public InputActionInfo(string localizationKey, BindingType type, int action)
            : this(localizationKey, localizationKey, type, action) { }

        public InputActionInfo(string localizationKey, string leftyLocalizationKey, BindingType type, int action)
        {
            LocalizationKey = localizationKey;
            LeftyLocalizationKey = leftyLocalizationKey;
            Type = type;
            Action = action;
        }
    }

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> GetTemplate(GameMode? mode)
        {
            return mode switch {
                GameMode.Menu => MENU,
                GameMode.FiveFretGuitar => FIVE_FRET_GUITAR,
                GameMode.SixFretGuitar => SIX_FRET_GUITAR,
                GameMode.FourLaneDrums => FOUR_LANE_DRUMKIT,
                GameMode.FiveLaneDrums => FIVE_LANE_DRUMKIT,
                GameMode.ProKeys => KEYS,
                _ => new()
            };
        }
    }
}
