using System;
using System.Collections.Generic;
using System.Text;
using YARG.Input.Serialization;

namespace YARG.Input.Bindings
{
    public class ReusableIntegerBinding : ReusableControlBinding<ReusableSingleIntegerBinding>
    {
        public ReusableIntegerBinding(InputActionInfo info) : base(info) { }

        public ReusableIntegerBinding(InputActionInfo info, List<string> controlNames) : base(info)
        {
            foreach (var controlName in controlNames)
            {
                Bindings.Add(new(controlName));
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

    public class ReusableSingleIntegerBinding : ReusableSingleBinding {
        public ReusableSingleIntegerBinding(string controlName) : base(controlName) { }

        public ReusableSingleIntegerBinding(SerializedSingleBinding serialized) : base(serialized)
        {
            DeserializeParameters(serialized.Parameters);
        }
    }
}
