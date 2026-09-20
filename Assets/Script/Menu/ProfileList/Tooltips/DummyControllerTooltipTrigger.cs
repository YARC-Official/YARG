using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.ProfileInfo;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class DummyControllerToolipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ProfilesMenu _profilesMenu;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { },
                new List<string>() { Localize.Key(_profilesMenu.CurrentBindingSetFilter.ToLocalizedNamePlural()) }
            );
        }
    }
}
