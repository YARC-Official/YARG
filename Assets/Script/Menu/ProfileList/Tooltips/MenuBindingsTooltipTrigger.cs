using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class MenuBindingsTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ControllerEntryView _entry;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var existenceString = _entry.MenuBindingSetsExist ? "Exist" : "NoneExist";

            var prefix = Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.MenuBindings", existenceString, "Prefix");
            var suffix = Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.MenuBindings", existenceString, "Suffix");

            return (
                new List<string>() { },
                new List<string>() { prefix, LayoutHelper.LayoutStringToControllerFamily(_entry.Controller.layout).ToLocalizedNamePluralSentence(), suffix }
            );
        }
    }
}
