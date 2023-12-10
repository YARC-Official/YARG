using System;
using System.Globalization;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace YARG.Menu
{
    [RequireComponent(typeof(TMP_InputField))]
    public class DurationInputField : MonoBehaviour
    {
        public enum Unit
        {
            Milliseconds,
            Seconds,
            Minutes,
            Hours
        }

        private static readonly Regex _unitRegex = new(@"(?!\d|\.|\,)+.*", RegexOptions.Compiled);

        [SerializeField]
        private Unit _preferredUnit;
        private double _duration;

        /// <summary>
        /// The unit that should be displayed along side the input.
        /// </summary>
        public Unit PreferredUnit
        {
            get => _preferredUnit;
            set
            {
                _preferredUnit = value;
                UpdateInputField();
            }
        }

        /// <summary>
        /// The maximum duration allowed to be entered in seconds.
        /// </summary>
        public double MaxValue { get; set; } = double.PositiveInfinity;

        /// <summary>
        /// The currently displayed duration of the input field in seconds.
        /// </summary>
        public double Duration
        {
            get => _duration;
            set
            {
                _duration = Math.Clamp(value, 0f, MaxValue);
                UpdateInputField();
            }
        }

        public UnityEvent<double> OnValueChanged;

        private TMP_InputField _inputField;

        private void Awake()
        {
            UpdateInputField();
        }

        private void UpdateInputField()
        {
            // Don't put this in awake because some values could be changed before the object
            // is fully spawned. Kinda hacky but it's not that bad.
            if (_inputField == null)
            {
                _inputField = GetComponent<TMP_InputField>();

                // Don't worry about removing the listener because it should be
                // destroyed along side the input field.
                _inputField.onEndEdit.AddListener(OnInputFieldEndEdit);
            }

            var realValue = Duration / GetMultiplierForUnit(PreferredUnit);

            _inputField.SetTextWithoutNotify(
                realValue.ToString(CultureInfo.InvariantCulture) + " " +
                GetUnitSuffix(PreferredUnit));
        }

        private void OnInputFieldEndEdit(string inputText)
        {
            try
            {
                // Split the number and the unit/suffix
                var suffixMatch = _unitRegex.Match(inputText);
                string suffix = suffixMatch.Value.Trim();
                string number = inputText[..suffixMatch.Index];

                double value = double.Parse(number, CultureInfo.InvariantCulture);

                // Convert to seconds
                var unit = FromUnitSuffix(suffix, PreferredUnit);
                value *= GetMultiplierForUnit(unit);

                Duration = value;
                OnValueChanged.Invoke(Duration);
            }
            catch
            {
                // Ignore error, and set the input field back to what it was before
                UpdateInputField();
            }
        }

        public static double GetMultiplierForUnit(Unit unit)
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

        private static string GetUnitSuffix(Unit unit)
        {
            return unit switch
            {
                Unit.Milliseconds => "ms",
                Unit.Seconds      => "s",
                Unit.Minutes      => "m",
                Unit.Hours        => "h",
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
                "s"  => Unit.Seconds,
                "m"  => Unit.Minutes,
                "h"  => Unit.Hours,
                _    => throw new Exception("Unreachable.")
            };
        }
    }
}