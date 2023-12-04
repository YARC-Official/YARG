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

        public override bool ValueEquals(object obj)
        {
            // Check if the null states are the same
            if (Value.HasValue != (obj != null))
            {
                return false;
            }

            // Check if one of them is null, they are both null do to the above statement.
            // The "obj == null" is mostly to suppress a warning.
            if (!Value.HasValue || obj == null)
            {
                return true;
            }

            // Check their types
            if (obj.GetType() != ValueType)
            {
                return false;
            }

            var a = ((Resolution?) obj).Value;
            return a.height == Value.Value.height &&
                a.width == Value.Value.width &&
                a.refreshRate == Value.Value.refreshRate;
        }
    }
}