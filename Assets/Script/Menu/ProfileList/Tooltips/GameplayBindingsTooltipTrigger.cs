using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class GameplayBindingsTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ControllerEntryView _entry;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var mode = _entry.Profile.GameMode;
            var family = LayoutHelper.LayoutStringToControllerFamily(_entry.Controller.layout);

            var existenceString = _entry.GameplayBindingSetsExist ? "Exist" : "NoneExist";

            var prefix = Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.GameplayBindings", existenceString, "Prefix");
            var suffix = Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.GameplayBindings", existenceString, "Suffix");

            return (
                new List<string>() { },
                new List<string>() { prefix, BindingSetHelper.GetDescriptiveName(mode, family), suffix }
            );
        }
    }
}
