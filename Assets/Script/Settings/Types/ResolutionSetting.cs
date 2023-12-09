using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class ResolutionSetting : DropdownSetting<Resolution?>
    {
        public ResolutionSetting(Action<Resolution?> onChange = null) : base(null, onChange)
        {
        }

        public override void UpdateValues()
        {
            _possibleValues.Clear();

            _possibleValues.Add(null);
            foreach (var resolution in Screen.resolutions)
            {
                _possibleValues.Add(resolution);
            }
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

        public override string ValueToString(Resolution? value) => value?.ToString() ?? "<i>Highest</i>";
    }
}