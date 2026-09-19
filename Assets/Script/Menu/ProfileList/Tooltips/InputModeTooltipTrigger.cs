using System.Collections.Generic;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class InputModeTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ProfileCenterPane _centerPane;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var modeString = _centerPane.Profile.GameMode.ToString();

            return (
                new List<string>(),
                new List<string>() {
                    Localize.Key("Enum.GameMode", modeString),
                    Localize.Key("Menu.ProfileList.Tooltip.InputMode", modeString)
                }
            );
        }
    }
}
