using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class ResolutionSetting : AbstractSetting<Resolution?>
    {
        public override string AddressableName => "Setting/Resolution";

        public ResolutionSetting(Action<Resolution?> onChange = null) : base(onChange)
        {
            _value = null;
        }

        public override bool ValueEquals(Resolution? value)
        {
            if (Value.HasValue && value.HasValue)
            {
                var v1 = Value.Value;
                var v2 = value.Value;
                return v1.height == v2.height &&
                    v1.width == v2.width &&
                    v1.refreshRate == v2.refreshRate;
            }

            return value.HasValue == Value.HasValue;
        }

        protected override bool ValueEquals(object obj)
        {
            if (obj is Resolution res)
                return ValueEquals(res);

            return obj is null && !Value.HasValue;
        }
    }
}