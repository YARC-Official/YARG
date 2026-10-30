using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.ProfileList;
using YARG.Menu.Tooltips;

namespace YARG.Assets.Script.Menu.ProfileList.Tooltips
{
    public class SingleBindingDropdownTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ReusableSingleBindView _bindView;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { },
                new List<string>() { _bindView.BindGroup.CenterPane.ProfilesMenu.CurrentBindingSetFilter.ToLocalizedNamePluralSentence() }
            );
        }
    }
}
