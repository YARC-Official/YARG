using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Core.Game;
using YARG.Input.Serialization;

namespace YARG.Input.Bindings
{
    public class ReusableIntegerBinding : ReusableControlBinding<ReusableSingleIntegerBinding, int>
    {
        public ReusableIntegerBinding(InputActionInfo info) : base(info) { }

        public ReusableIntegerBinding(ReusableIntegerBinding original) : base(original) {
            foreach (var binding in original.Bindings)
            {
                AddBinding(new(binding));
            }
        }

        public ReusableIntegerBinding(SerializedReusableControlBinding serialized, InputActionInfo info) : base(serialized, info)
        {
            foreach (var binding in serialized.Controls)
            {
                AddBinding(new(binding));
            }
        }
    }
}
