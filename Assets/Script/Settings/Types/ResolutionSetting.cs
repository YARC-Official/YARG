using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class ResolutionSetting : AbstractSetting<Resolution?>
    {
        private Resolution? _data;

        public override Resolution? Data
        {
            get => _data;
            set
            {
                _data = value;
                base.Data = value;
            }
        }

        public override string AddressableName => "Setting/Resolution";

        public ResolutionSetting(Action<Resolution?> onChange = null) : base(onChange)
        {
            _data = null;
        }

        public override bool IsSettingDataEqual(object obj)
        {
            // Check if the null states are the same
            if (Data.HasValue != (obj != null))
            {
                return false;
            }

            // Check if one of them is null, they are both null do to the above statement.
            // The "obj == null" is mostly to suppress a warning.
            if (!Data.HasValue || obj == null)
            {
                return true;
            }

            // Check their types
            if (obj.GetType() != DataType)
            {
                return false;
            }

            var a = ((Resolution?) obj).Value;
            return a.height == Data.Value.height &&
                a.width == Data.Value.width &&
                a.refreshRate == Data.Value.refreshRate;
        }
    }
}