using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Localization;
using YARG.Menu.ProfileList;
using YARG.Menu.Tooltips;

namespace YARG.Menu.ProfileList
{
    public class IntegerBindGroupToolipTrigger : TooltipTrigger
    {
        [SerializeField]
        private ReusableIntegerBindGroup _bindGroup;

        protected override (IReadOnlyList<string> titleParams, IReadOnlyList<string> textParams) GetParameters()
        {
            return (
                new List<string>() { Localize.Key(_bindGroup.Binding.Name) },
                new List<string>() { }
            );
        }
    }
}
