using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileInfo;

namespace YARG.Menu.ProfileList
{
    public class ReusableIntegerBindGroup
        : ReusableBindGroup<ReusableSingleIntegerBindView, ReusableIntegerBinding, ReusableSingleIntegerBinding, int>
    {
        [Space]
        [SerializeField]
        private TMP_InputField _valueText;
    }
}
