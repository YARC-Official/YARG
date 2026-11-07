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

            return (
                new List<string>() { },
                new List<string>() { BindingSetHelper.GetDescriptiveName(mode, family) }
            );
        }
    }
}
