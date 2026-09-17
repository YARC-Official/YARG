using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YARG.Input.Bindings
{
    public class RuntimeAxisBinding : RuntimeControlBinding<RuntimeSingleAxisBinding, float>
    {
        public float State { get; protected set; }

        public RuntimeAxisBinding(InputDevice controller, ReusableAxisBinding reusableBinding)
            : base(reusableBinding.Action, reusableBinding.Name)
        {
            foreach (var singleBinding in reusableBinding.Bindings)
            {
                var singleRuntimeBinding = singleBinding.MakeRuntime(controller) as RuntimeSingleAxisBinding;

                if (singleRuntimeBinding is not null)
                {
                    _bindings.Add(singleRuntimeBinding);
                }
            }
        }

        public override bool IsControlActuated(InputControl<float> control)
        {
            float previousValue = control.ReadValueFromPreviousFrame();
            float value = control.ReadValue();
            return Math.Abs(value - previousValue) >= AXIS_DELTA_THRESHOLD;
        }

        protected override void OnStateChanged(RuntimeSingleAxisBinding singleBinding, double time)
        {
            var max = 0f;
            foreach (var binding in _bindings)
            {
                var value = binding.State;
                if (Math.Abs(value) > Math.Abs(max))
                {
                    max = value;
                }

                // Ignore if state is unchanged
                if (Mathf.Approximately(State, max))
                {
                    return;
                }

                State = max;
                FireInputEvent(time, max);
            }
        }
    }
}
