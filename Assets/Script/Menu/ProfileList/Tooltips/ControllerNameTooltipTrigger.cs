using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Helpers;
using YARG.Localization;
using YARG.Menu.ProfileList;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class ControllerNameTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ControllerEntryView _entry;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var familyString = LayoutHelper.LayoutStringToControllerFamily(_entry.Controller.layout).ToLocalizedNameSingularSentence();

            return (
                new List<string>() { _entry.Controller.displayName },
                new List<string>() { familyString }
            );
        }
    }
}
