using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Serialization;
using YARG.Localization;

namespace YARG.Input.Bindings
{
    public abstract class ReusableControlBinding
    {
        public string Name { get; set; }
        public string NameLefty { get; set; }
        public int Action { get; }

        public event Action Changed;
        protected void NotifyChanged()
        {
            Changed?.Invoke();
        }

        public InputActionInfo Info { get; private set; }

        public ReusableControlBinding(InputActionInfo info)
        {
            Name = Localize.Key("Bindings", info.Key);
            NameLefty = Localize.Key("Bindings", info.LeftyLocalizationKey);
            Action = info.Action;
            Info = info;
        }

        public ReusableControlBinding(ReusableControlBinding original)
        {
            Name = original.Name;
            NameLefty = original.NameLefty;
            Action = original.Action;
            Info = original.Info;
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

    public abstract class ReusableControlBinding<TSingle, TSingleState> : ReusableControlBinding
        where TSingle : ReusableSingleBinding<TSingleState>
        where TSingleState : struct
    {
        protected List<TSingle> _bindings = new();
        public IReadOnlyList<TSingle> Bindings => _bindings;

        public ReusableControlBinding(InputActionInfo info) : base(info) { }

        public ReusableControlBinding(ReusableControlBinding<TSingle, TSingleState> original) : base(original) {}

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

        public void AddBinding(TSingle single)
        {
            single.Changed += NotifyChanged;
            _bindings.Add(single);
            NotifyChanged();
        }

        public void RemoveBinding(TSingle single)
        {
            single.Changed -= NotifyChanged;
            _bindings.Remove(single);
            NotifyChanged();
        }

        public void ClearBindings()
        {
            foreach (var single in _bindings)
            {
                RemoveBinding(single);
            }
        }
    }
}
