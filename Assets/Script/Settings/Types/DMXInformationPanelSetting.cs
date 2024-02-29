using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class DMXInformationPanelSetting : AbstractSetting<int>
    {
        public override string AddressableName => "Setting/DMXInformationPanel";


        public DMXInformationPanelSetting(int value, int min = int.MinValue, int max = int.MaxValue, Action<int> onChange = null) :
            base(onChange)
        {

            _value = value;
        }

        protected override void SetValue(int value)
        {

        }

        public override bool ValueEquals(int value) => value == Value;
    }
}