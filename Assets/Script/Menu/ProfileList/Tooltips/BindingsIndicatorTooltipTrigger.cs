using System.Collections.Generic;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class BindingsIndicatorTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private BindingsIndicator _indicator;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var litString = _indicator.Lit ? "Lit" : "Unlit";

            return (
                new List<string>(),
                new List<string>() { Localize.Key("Menu.ProfileList.Tooltip", _localizationKey, litString) }
            );
        }
    }
}
