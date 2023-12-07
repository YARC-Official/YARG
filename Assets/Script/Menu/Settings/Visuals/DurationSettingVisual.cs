using System;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

using Unit = YARG.Settings.Types.DurationSetting.Unit;

namespace YARG.Menu.Settings.Visuals
{
    public class DurationSettingVisual : BaseSettingVisual<DurationSetting>
    {
        private static readonly Regex _unitRegex = new(@"(?!\d|\.|\,)+.*", RegexOptions.Compiled);

        [SerializeField]
        private TMP_InputField _inputField;

        protected override void RefreshVisual()
        {
            var realValue = Setting.Value / GetMultiplierForUnit(Setting.PreferredUnit);

            _inputField.text =
                realValue.ToString(CultureInfo.InvariantCulture) + " " +
                GetUnitSuffix(Setting.PreferredUnit);
        }

        public override NavigationScheme GetNavigationScheme()
        {
            return new NavigationScheme(new()
            {
                NavigateFinish,
                new NavigationScheme.Entry(MenuAction.Up, "Increase", () =>
                {
                    Setting.Value += GetMultiplierForUnit(Setting.PreferredUnit);
                    RefreshVisual();
                }),
                new NavigationScheme.Entry(MenuAction.Down, "Decrease", () =>
                {
                    Setting.Value -= GetMultiplierForUnit(Setting.PreferredUnit);
                    RefreshVisual();
                })
            }, true);
        }

        public void OnTextFieldChange()
        {
            try
            {
                // Split the number and the unit/suffix
                var suffixMatch = _unitRegex.Match(_inputField.text);
                string suffix = suffixMatch.Value.Trim();
                string number = _inputField.text[..suffixMatch.Index];

                double value = double.Parse(number, CultureInfo.InvariantCulture);

                // Convert to seconds
                var unit = FromUnitSuffix(suffix, Setting.PreferredUnit);
                value *= GetMultiplierForUnit(unit);

                value = Math.Clamp(value, 0f, Setting.Max);
                Setting.Value = value;
            }
            catch
            {
                // Ignore error
            }

            RefreshVisual();
        }

        private static string GetUnitSuffix(Unit unit)
        {
            return unit switch
            {
                Unit.Milliseconds => "ms",
                Unit.Seconds      => "s",
                Unit.Minutes      => "m",
                Unit.Hours        => "h",
                _ => throw new Exception("Unreachable.")
            };
        }

        private static double GetMultiplierForUnit(Unit unit)
        {
            return unit switch
            {
                Unit.Milliseconds => 0.001,
                Unit.Seconds      => 1.0,
                Unit.Minutes      => 60.0,
                Unit.Hours        => 3600.0,
                _                 => throw new Exception("Unreachable.")
            };
        }

        private static Unit FromUnitSuffix(string unit, Unit defaultUnit)
        {
            if (string.IsNullOrEmpty(unit))
            {
                return defaultUnit;
            }

            return unit.ToLowerInvariant() switch
            {
                "ms" => Unit.Milliseconds,
                "s" => Unit.Seconds,
                "m" => Unit.Minutes,
                "h" => Unit.Hours,
                _  => throw new Exception("Unreachable.")
            };
        }
    }
}