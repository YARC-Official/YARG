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
            return (
                new List<string>() { },
                new List<string>() { LayoutHelper.LayoutStringToControllerFamily(_entry.Controller.layout).ToLocalizedNamePluralSentence() }
            );
        }
    }
}
