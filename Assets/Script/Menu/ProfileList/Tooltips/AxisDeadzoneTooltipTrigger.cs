using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.ProfileList;
using YARG.Menu.Tooltips;

namespace YARG.Assets.Script.Menu.ProfileList.Tooltips
{
    public class AxisDeadzoneTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ReusableSingleAxisBindView _bindView;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { _bindView.SingleBinding.LowerDeadzone.ToString(), _bindView.SingleBinding.UpperDeadzone.ToString() },
                new List<string>() { }
            );
        }
    }
}
