using System.Collections.Generic;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class ProfileListHeaderTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ProfileListHeaderView _profileListHeaderView;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var key = _profileListHeaderView.Key;

            return (
                new List<string>() { _profileListHeaderView.Text },
                new List<string>() { Localize.Key("Menu.ProfileList.Tooltip.Profiles.List.Header", _profileListHeaderView.Key) }
            );
        }
    }
}
