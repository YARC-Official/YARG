using System;
using System.Collections.Generic;
using System.Text;
using YARG.Input.Serialization;

namespace YARG.Input
{
    public class ReusableControlBinding
    {
        public Dictionary<string, string> Parameters;
        public List<SerializedInputControl> Controls;
        public BindingType Type;

        public ReusableControlBinding(BindingType type, SerializedReusableControlBinding? serialized = null)
        {
            Type = type;
            Parameters = serialized?.Parameters ?? new();
            Controls = serialized?.Controls ?? new();
        }

        public SerializedReusableControlBinding Serialize()
        {
            return new()
            {
                Parameters = Parameters ?? new(),
                Controls = Controls ?? new()
            };
        }
    }
}
