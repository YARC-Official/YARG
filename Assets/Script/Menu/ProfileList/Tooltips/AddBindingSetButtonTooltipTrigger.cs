using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class AddBindingSetButtonTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private BindingSetListHeaderView _headerView;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { },
                new List<string>() { BindingSetHelper.GetDescriptiveName(_headerView.Mode, _headerView.Family) }
            );
        }
    }
}
