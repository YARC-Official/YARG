using System;
using System.Collections.Generic;
using YARG.Core.Logging;
using YARG.Input.Serialization;
using YARG.Localization;

namespace YARG.Input.Bindings
{
    public abstract class ReusableControlBinding
    {
        public string Name { get; set; }
        public string NameLefty { get; set; }
        public int Action { get; }

        public ReusableControlBinding(InputActionInfo info)
        {
            Name = Localize.Key("Bindings", info.LocalizationKey);
            NameLefty = Localize.Key("Bindings", info.LeftyLocalizationKey);
            Action = info.Action;
        }

        public abstract SerializedReusableControlBinding Serialize();

        // Override this for binding types that have parameters to serialize
        protected virtual Dictionary<string, string> SerializeParameters()
        {
            return new();
        }


        // Override this for binding types that have parameters to deserialize
        protected virtual void DeserializeParameters(Dictionary<string, string> parameters)
        {
            foreach (var (key, val) in parameters)
            {
                LogUnknownParameter(key, val);
            }
        }


        public static void LogParseFailure(string key, string val, string type, object def)
        {
            YargLogger.LogWarning($"Failed to parse {key} value {val} as {type}; using default {def} instead");
        }

        public static void LogUnknownParameter(string key, string val)
        {
            YargLogger.LogWarning($"Found unrecognized single binding parameter {key} with value {val}; ignoring");
        }
    }

    public abstract class ReusableControlBinding<TSingle> : ReusableControlBinding
        where TSingle : ReusableSingleBinding
    {
        public List<TSingle> Bindings = new();

        public ReusableControlBinding(InputActionInfo info) : base(info) { }

        public ReusableControlBinding(SerializedReusableControlBinding serialized, InputActionInfo info) : base(info)
        {
            DeserializeParameters(serialized.Parameters);
        }

        public override SerializedReusableControlBinding Serialize()
        {
            var serializedControls = new List<SerializedSingleBinding>();
            foreach (var binding in Bindings)
            {
                serializedControls.Add(binding.Serialize());
            }

            var serializedParameters = SerializeParameters();

            return new()
            {
                Controls = serializedControls,
                Parameters = serializedParameters
            };
        }
    }

    public abstract class ReusableSingleBinding
    {
        public string ControlName { get; set; }
        public string DisplayName { get; set; }

        public ReusableSingleBinding(string controlName, string displayName)
        {
            ControlName = controlName;
            DisplayName = displayName;
        }

        public ReusableSingleBinding(SerializedSingleBinding serialized)
            : this(serialized.ControlName, serialized.ControlName) { } // TODO-FRICK: DisplayName

        public SerializedSingleBinding Serialize()
        {
            return new SerializedSingleBinding(ControlName)
            {
                Parameters = SerializeParameters()
            };
        }

        protected virtual Dictionary<string, string> SerializeParameters()
        {
            return new();
        }

        // Override this for single binding types that have parameters to parse
        protected virtual void DeserializeParameters(Dictionary<string, string> parameters)
        {
            foreach (var (key, val) in parameters)
            {
                ReusableControlBinding.LogUnknownParameter(key, val);
            }
        }       
    }
}
