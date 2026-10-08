using UnityEditor.Experimental.GraphView;
using UnityEngine.InputSystem;
using YARG.Helpers;

namespace YARG.Input.Bindings
{
    public class RuntimeSingleAxisBinding : RuntimeSingleBinding<float>
    {
        public float RawState { get; private set; }

        public bool Inverted { get; }
        public float Minimum { get; }
        public float Maximum { get; }
        public float UpperDeadzone { get; }
        public float LowerDeadzone { get; }

        public RuntimeSingleAxisBinding(InputControl<float> control, ReusableSingleAxisBinding reusableBinding)
            : base(control)
        {
            Inverted = reusableBinding.Inverted;
            Minimum = reusableBinding.Minimum;
            Maximum = reusableBinding.Maximum;
            UpperDeadzone = reusableBinding.UpperDeadzone;
            LowerDeadzone = reusableBinding.LowerDeadzone;
        }

        public override void UpdateState(double time, float value)
        {
            RawState = value;
            State = BindingSetHelper.CalculateCalibratedAxisValue(RawState, Minimum, Maximum, UpperDeadzone, LowerDeadzone, Inverted);
            InvokeStateChanged(State);
        }

        public override void ResetState()
        {
            RawState = default;
            State = default;
            InvokeStateChanged(State);
        }
    }
}
