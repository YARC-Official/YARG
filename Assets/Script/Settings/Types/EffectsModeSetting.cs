using System;

namespace YARG.Settings.Types
{
    public sealed class EffectsModeSetting : AbstractSetting<EffectsMode>, IToggleSetting
    {
        public override string AddressableName => "Setting/Toggle";

        bool IToggleSetting.Value
        {
            get => Value == EffectsMode.Quality;
            set => Value = value ? EffectsMode.Quality : EffectsMode.Performance;
        }

        public EffectsModeSetting(Action<EffectsMode> onChange) : base(onChange)
        {
            _value = EffectsMode.Performance;
        }

        public override bool ValueEquals(EffectsMode value) => Value == value;
    }
}
