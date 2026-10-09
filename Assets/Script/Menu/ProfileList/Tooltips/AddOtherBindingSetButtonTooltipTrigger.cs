using System.Collections.Generic;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class AddOtherBindingSetButtonTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private BindingSetListFooterView _footerView;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            const string header = "Menu.ProfileList.Tooltip.Bindings.List.AddOtherBindingSet.";

            var text = _footerView.ProfilesMenu.CurrentBindingSetFilter switch
            {
                ControllerFamily.Other => Localize.Key($"{header}Generic"),
                ControllerFamily.Gamepad or
                ControllerFamily.ComputerKeyboard or
                ControllerFamily.MidiDevice => Localize.KeyFormat($"{header}Flexible", _footerView.ProfilesMenu.CurrentBindingSetFilter.ToLocalizedNamePluralSentence()),
                _ => Localize.KeyFormat($"{header}NotRecommended", _footerView.ProfilesMenu.CurrentBindingSetFilter.ToLocalizedNamePluralSentence())
            };


            return (
                new List<string>(),
                new List<string>() { text }
            );
        }
    }
}
