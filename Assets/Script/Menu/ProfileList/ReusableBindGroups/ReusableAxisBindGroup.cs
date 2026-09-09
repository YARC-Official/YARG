using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableAxisBindGroup
        : ReusableBindGroup<ReusableSingleAxisBindView, ReusableAxisBinding, ReusableSingleAxisBinding>
    {
        [Space]
        [SerializeField]
        private AxisDisplay _valueDisplay;
    }
}
