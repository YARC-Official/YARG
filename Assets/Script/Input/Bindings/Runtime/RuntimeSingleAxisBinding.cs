using UnityEditor.Experimental.GraphView;
using UnityEngine.InputSystem;

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

        public override void UpdateState(double time)
        {
            RawState = Control.value;
            State = CalculateState(RawState);
            InvokeStateChanged(State);
        }

        public override void ResetState()
        {
            RawState = default;
            State = default;
            InvokeStateChanged(State);
        }

        private float CalculateState(float rawValue)
        {
            float max;
            float min;
            float @base;

            if (rawValue > UpperDeadzone)
            {
                max = Maximum;
                min = UpperDeadzone;
                @base = 0;
            }
            else if (rawValue < LowerDeadzone)
            {
                max = LowerDeadzone;
                min = Minimum;
                @base = -1;
            }
            else
            {
                return 0;
            }

            float percentage = (rawValue - min) / (max - min);
            float value = @base + percentage;
            if (float.IsNaN(value))
                value = 0;

            value *= Inverted ? -1 : 1;
            return value;
        }
    }
}
