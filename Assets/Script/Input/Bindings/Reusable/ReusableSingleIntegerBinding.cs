using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Input.Serialization;

namespace YARG.Input.Bindings
{
    public class ReusableSingleIntegerBinding : ReusableSingleBinding<int>
    {
        public ReusableSingleIntegerBinding() : base(null, null, null) { }

        public ReusableSingleIntegerBinding(string controlName, string displayName, string sourceLayout) : base(controlName, displayName, sourceLayout) { }

        public ReusableSingleIntegerBinding(ReusableSingleIntegerBinding original) : base(original) { }

        public ReusableSingleIntegerBinding(SerializedSingleBinding serialized) : base(serialized)
        {
            DeserializeParameters(serialized.Parameters);
        }

        protected override bool IsControlActuated(InputControl<int> control)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();

            return Math.Abs(value - previousValue) >= RuntimeIntegerBinding.INTEGER_DELTA_THRESHOLD;
        }

        protected override RuntimeSingleBinding<int> MakeRuntime(InputControl<int> control)
        {
            return new RuntimeSingleIntegerBinding(control, this);
        }
    }
}
