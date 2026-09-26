using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

namespace YARG.Input.Bindings
{
    public class RuntimeIntegerBinding : RuntimeControlBinding<RuntimeSingleIntegerBinding, int>
    {
        private const int INTEGER_DELTA_THRESHOLD = 1;

        public int State { get; protected set; }

        public RuntimeIntegerBinding(InputDevice controller, ReusableIntegerBinding reusableBinding)
            : base(reusableBinding.Action, reusableBinding.Name)
        {
            foreach (var singleBinding in reusableBinding.Bindings)
            {
                var singleRuntimeBinding = singleBinding.MakeRuntime(controller) as RuntimeSingleIntegerBinding;

                if (singleRuntimeBinding is not null)
                {
                    _bindings.Add(singleRuntimeBinding);
                }
            }
        }

        public override bool IsControlActuated(InputControl<int> control)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();
            return Math.Abs(value - previousValue) >= INTEGER_DELTA_THRESHOLD;
        }

        protected override void OnStateChanged(RuntimeSingleIntegerBinding singleBinding, double time)
        {
            int max = 0;
            foreach (var binding in _bindings)
            {
                var value = binding.State;
                if (value > max)
                {
                    max = value;
                }
            }

            // Ignore if state is unchanged
            if (State == max)
            {
                return;
            }

            State = max;
            FireInputEvent(time, max);
        }
    }
}
