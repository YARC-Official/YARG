using System;
using Unit = YARG.Menu.DurationInputField.Unit;

namespace YARG.Settings.Types
{
    public class DurationSetting : AbstractSetting<double>
    {
        public override string AddressableName => "Setting/Duration";

        public Unit PreferredUnit { get; private set; }
        public double Max { get; private set; }

        public DurationSetting(double value, Unit preferredUnit, double max = double.PositiveInfinity,
            Action<double> onChange = null) : base(onChange)
        {
            PreferredUnit = preferredUnit;
            Max = max;

            _value = value;
        }

        protected override void SetValue(double value)
        {
            _value = Math.Clamp(value, 0, Max);
        }

        public override bool ValueEquals(double value)
            => Math.Abs(value - Value) < double.Epsilon;
    }
}