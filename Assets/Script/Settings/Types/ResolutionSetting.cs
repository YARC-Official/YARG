using System;
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
                    v1.refreshRateRatio.numerator == v2.refreshRateRatio.numerator &&
                    v1.refreshRateRatio.denominator == v2.refreshRateRatio.denominator;
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
            Resolution resolution;
            bool isDefault = value == null;

            // Newer unity doesn't format the Resolution string so nicely, so we have to do it ourselves
            if (isDefault)
            {
                resolution = ScreenHelper.GetScreenResolution();
            }
            else
            {
                resolution = value.Value;
            }

            var refresh = resolution.refreshRateRatio.value;
            var resolutionString = $"{resolution.width} x {resolution.height} @ {refresh:0.##}Hz";

            if (isDefault)
            {
                return Localize.KeyFormat("Settings.Setting.Resolution.Default", resolutionString);
            }

            return resolutionString;
        }
    }
}