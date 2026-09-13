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

        public ReusableIntegerBinding(InputActionInfo info, List<string> controlNames) : base(info)
        {
            foreach (var controlName in controlNames)
            {
                Bindings.Add(new(controlName, controlName)); // TODO-FRICK: DisplayName
            }
        }

        public ReusableIntegerBinding(SerializedReusableControlBinding serialized, InputActionInfo info) : base(serialized, info)
        {
            foreach (var binding in serialized.Controls)
            {
                Bindings.Add(new(binding));
            }
        }
    }

    public class ReusableSingleIntegerBinding : ReusableSingleBinding<int> {
        public ReusableSingleIntegerBinding(string controlName, string displayName) : base(controlName, displayName) { }

        public ReusableSingleIntegerBinding(SerializedSingleBinding serialized) : base(serialized)
        {
            DeserializeParameters(serialized.Parameters);
        }

        protected override RuntimeSingleBinding<int> MakeRuntime(InputControl<int> control)
        {
            throw new NotImplementedException(); // TODO-FRICK
        }
    }
}
