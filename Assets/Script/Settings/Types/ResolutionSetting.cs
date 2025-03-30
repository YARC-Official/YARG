using System;
using System.Linq;
using UnityEngine;
using YARG.Helpers;
using YARG.Localization;

namespace YARG.Settings.Types
{
    public class ResolutionSetting : DropdownSetting<Resolution?>
    {
        public ResolutionSetting(Action<Resolution?> onChange = null) : base(null, onChange, localizable: false)
        {
        }

        public override void UpdateValues()
        {
            _possibleValues.Clear();

            // Add all of the resolutions
            foreach (var resolution in Screen.resolutions)
            {
                _possibleValues.Add(resolution);
            }

            // Reverse so it's listed as highest to lowest
            _possibleValues.Reverse();

            // Insert the "Highest" option
            _possibleValues.Insert(0, null);
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

        public override string ValueToString(Resolution? value)
        {
            return value?.ToString() ?? Localize.KeyFormat(
                "Settings.Setting.Resolution.Default", ScreenHelper.GetScreenResolution()
            );
        }
    }
}