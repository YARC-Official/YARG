using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Menu.ProfileList
{
    public class ReusableSingleIntegerBindView : ReusableSingleBindView<ReusableIntegerBinding, ReusableSingleIntegerBinding, int>
    {
        [SerializeField]
        private TMP_InputField _valueText;
    }
}
