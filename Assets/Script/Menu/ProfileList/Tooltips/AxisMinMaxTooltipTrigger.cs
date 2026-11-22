using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Menu.ProfileList;
using YARG.Menu.Tooltips;

namespace YARG.Assets.Script.Menu.ProfileList.Tooltips
{
    public class AxisMinMaxTooltipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ReusableSingleAxisBindView _bindView;
        [SerializeField]
        private bool _isMax;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            var text = (_isMax ? _bindView.SingleBinding.Maximum : _bindView.SingleBinding.Minimum).ToString();

            return (
                new List<string>() { text },
                new List<string>() { text }
            );
        }
    }
}
