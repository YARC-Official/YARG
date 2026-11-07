using System.Collections.Generic;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class AxisBindGroupHeaderToolipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ReusableAxisBindGroup _bindGroup;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { _bindGroup.CenterPane.ShowLeftyNames ? _bindGroup.Binding.NameLefty : _bindGroup.Binding.Name },
                new List<string>() { }
            );
        }
    }
}
