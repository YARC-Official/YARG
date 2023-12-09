using System;
using HitWindowPreset = YARG.Core.Game.EnginePreset.HitWindowPreset;

namespace YARG.Settings.Types
{
    public class HitWindowSetting : AbstractSetting<HitWindowPreset>
    {
        public override string AddressableName => "Setting/HitWindow";

        public HitWindowSetting(HitWindowPreset value, Action<HitWindowPreset> onChange = null) :
            base(onChange)
        {
            _value = value;
        }

        public override bool ValueEquals(HitWindowPreset value)
        {
            return value.IsDynamic == Value.IsDynamic &&
                Math.Abs(value.MaxWindow - Value.MaxWindow) < double.Epsilon &&
                Math.Abs(value.MinWindow - Value.MaxWindow) < double.Epsilon &&
                Math.Abs(value.FrontToBackRatio - Value.FrontToBackRatio) < double.Epsilon;
        }
    }
}