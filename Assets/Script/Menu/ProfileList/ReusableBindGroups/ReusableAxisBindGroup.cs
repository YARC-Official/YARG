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
        : ReusableBindGroup<ReusableSingleAxisBindView, ReusableAxisBinding, float, ReusableSingleAxisBinding, float>
    {
        [Space]
        [SerializeField]
        private AxisDisplay _valueDisplay;

        protected override void UpdateDisplay()
        {
            var maxMagnitude = 0f;

            foreach (var state in _states)
            {
                if (Math.Abs(state) > maxMagnitude)
                {
                    maxMagnitude = state;
                }
            }

            _valueDisplay.Value = maxMagnitude;
        }
    }
}
