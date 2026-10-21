using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;

namespace YARG.Input.Bindings
{
    public class RuntimeSingleIntegerBinding : RuntimeSingleBinding<int>
    {
        public RuntimeSingleIntegerBinding(InputControl<int> control, ReusableSingleIntegerBinding reusableBinding)
            : base(control)
        {
            // No parameters to copy
        }
    }
}
