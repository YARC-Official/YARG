using System.Collections.Generic;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class JumpToButtonTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ControllerEntryView _entry;
        [SerializeField]
        private bool _isMenu;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var bindingSet = _isMenu ? _entry.MenuBindingSet : _entry.GameplayBindingSet;

            return (
                new List<string>() { },
                new List<string>() { bindingSet is null ?
                    Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Unpopulated") :
                    Localize.KeyFormat("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Populated", _entry.Controller.displayName)
                }
            );
        }
    }
}
