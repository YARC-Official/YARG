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

            var modeString = mode.ToLocalizedNameShort();
            var preposition = mode is GameMode.Vocals ? "with" : "on";
            var controllerString = LayoutHelper.LayoutStringToControllerFamily(_entry.Controller.layout).ToLocalizedNamePluralSentence();

            return (
                new List<string>() { },
                new List<string>() { modeString, preposition, controllerString }
            );
        }
    }
}
