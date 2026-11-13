using System.Collections.Generic;
using UnityEngine;
using YARG.Helpers;
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

            string title;
            string text;

            if (bindingSet is null)
            {
                var suffix = _isMenu ?
                    Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Unpopulated.MenuSuffix") :
                    $"{_entry.Profile.GameMode.ToLocalizedNameShort()} {Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Unpopulated.GameplaySuffix")}";

                title = Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Unpopulated.Title");
                text = Localize.KeyFormat("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Unpopulated.Text", _entry.Controller.GetFamily(), suffix);
            }
            else
            {
                title = Localize.Key("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Populated.Title");
                text = Localize.KeyFormat("Menu.ProfileList.Tooltip.Profiles.Settings.JumpTo.Populated.Text", _entry.Controller.displayName);
            }

            return (
                new List<string>() { title },
                new List<string>() { text }
            );
        }
    }
}
