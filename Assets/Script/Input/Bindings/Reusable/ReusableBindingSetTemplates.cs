using System;
using System.Collections.Generic;
using System.Text;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public enum BindingType
    {
        Button,
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
        public static Dictionary<string, InputActionInfo> GetTemplate(ControllerFamily controllerFamily)
        {
            return controllerFamily switch {
                ControllerFamily.FiveFretGuitar => FIVE_FRET_GUITAR,
                _ => new()
            };
        }
    }
}
