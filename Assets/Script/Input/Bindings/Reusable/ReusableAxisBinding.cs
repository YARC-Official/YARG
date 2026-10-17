using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor.Experimental.GraphView;
using UnityEngine.InputSystem;
using YARG.Core.Game;
using YARG.Helpers;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public class ReusableAxisBinding : ReusableControlBinding<ReusableSingleAxisBinding, float>
    {
        public ReusableAxisBinding(InputActionInfo info) : base(info) { }

        public ReusableAxisBinding(InputActionInfo info, ReusableSingleAxisBindingConfig control)
            : this(info, new List<ReusableSingleAxisBindingConfig>() { control }) { }

        public ReusableAxisBinding(ReusableAxisBinding original) : base(original) {
            foreach (var binding in original.Bindings)
            {
                AddBinding(new(binding));
            }
        }

        public ReusableAxisBinding(InputActionInfo info, List<ReusableSingleAxisBindingConfig> controls) : base(info)
        {
            foreach (var controlConfig in controls)
            {
                AddBinding(new(controlConfig));
            }
        }

        public ReusableAxisBinding(SerializedReusableControlBinding serialized, InputActionInfo info) : base(serialized, info) {
            foreach (var binding in serialized.Controls)
            {
                AddBinding(new(binding));
            }
        }
    }

    public struct ReusableSingleAxisBindingConfig
    {
        public string ControlPath;
        public string DisplayName;
        public string SourceLayout;
        public bool? Inverted;
        public float? Maximum;
        public float? Minimum;
        public float? LowerDeadzone;
        public float? UpperDeadzone;

        public ReusableSingleAxisBindingConfig(ControllerFamily family, string controlPath)
        {
            ControlItemInfo control = LayoutHelper.GetControlInfo(family, controlPath);

            ControlPath = control.ControlPath;
            DisplayName = control.DisplayName;
            SourceLayout = LayoutHelper.ControllerFamilyToLayoutString(family);

            // These would be more pleasant as struct field initializers, but those aren't in C# 9.0
            Inverted = null;
            Maximum = null;
            Minimum = null;
            LowerDeadzone = null;
            UpperDeadzone = null;

        }
    }
}
