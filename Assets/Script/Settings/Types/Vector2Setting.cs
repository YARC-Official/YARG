using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class Vector2Setting : AbstractSetting<Vector2>
    {
        public Vector2Setting(Action<Vector2> onChange) : base(onChange)
        {
        }

        public override string AddressableName => "Setting/Vector2";

        public override bool ValueEquals(Vector2 value) => value == Value;
    }
}