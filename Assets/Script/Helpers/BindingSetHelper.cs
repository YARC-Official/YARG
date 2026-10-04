using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
using YARG.Core.Game;
using YARG.Input.Bindings;
using YARG.Player;

namespace YARG.Helpers
{
    public static class BindingSetHelper
    {
        public static (List<YargProfile> allUsers, List<YargProfile> activeUsers) GetUsersOfBindingSet(ReusableBindingSet bindingSet)
        {
            var allUsers = new List<YargProfile>();
            var activeUsers = new List<YargProfile>();


            foreach (var deviceInfo in BindingsContainer.AllPlayerDeviceInfo)
            {
                // Check preferred binding sets (one per (ControllerFamily,GameMode) tuple)
                var profileBindings = deviceInfo.AllPreferredBindingSets;
                if (profileBindings.Contains(bindingSet))
                {
                    allUsers.Add(deviceInfo.Profile);

                    if (PlayerContainer.IsProfileTaken(deviceInfo.Profile))
                    {
                        activeUsers.Add(deviceInfo.Profile);
                    }

                    continue;
                }

                // If a connected player has multiple controllers of the same family at the same
                // time, then some of them might be using other binding sets besides that player's
                // general (ControllerFamily,GameMode)-wide preference
                if (deviceInfo.BindingSetsInUse.Contains(bindingSet))
                {
                    allUsers.Add(deviceInfo.Profile);

                    // Disconnected profiles will always have an empty BindingSetsInUse, so no need
                    // to check IsProfileTaken
                    activeUsers.Add(deviceInfo.Profile); ;
                }
            }

            return (allUsers, activeUsers);
        }

        public static Func<InputControl, bool> GetQuickBindScreener(BindingType bindingType)
        {
            return bindingType switch
            {
                BindingType.Button or BindingType.IndividualButton or BindingType.DrumButton => IsButtonBeingQuickBound,
                BindingType.Axis => IsAxisBeingQuickBound,
                BindingType.Integer => IsIntegerBeingQuickBound,
                _ => throw new ArgumentOutOfRangeException("Unexpected binding type")
            };
        }

        public static string TrimControllerName(InputControl control, InputDevice controller)
        {
            return control.path[(controller.path.Length)..].TrimStart('/');
        }

        private static bool IsButtonBeingQuickBound(InputControl control) {
            if (control is not InputControl<float> floatControl)
            {
                return false;
            }

            float previousValue = floatControl.ReadValueFromPreviousFrame();
            float value = floatControl.ReadValue();
            bool actuated = Math.Abs(value - previousValue) >= RuntimeControlBinding.AXIS_DELTA_THRESHOLD;

            if (floatControl is ButtonControl button)
            {
                return actuated && value >= button.pressPointOrDefault;
            }
            else
            {
                return actuated;
            }
        }

        private static bool IsAxisBeingQuickBound(InputControl control)
        {
            if (control is not InputControl<float> floatControl)
            {
                return false;
            }

            float previousValue = floatControl.ReadValueFromPreviousFrame();
            float value = floatControl.ReadValue();

            return Math.Abs(value - previousValue) >= RuntimeControlBinding.AXIS_DELTA_THRESHOLD;
        }

        private static bool IsIntegerBeingQuickBound(InputControl control)
        {
            if (control is not InputControl<int> integerControl)
            {
                return false;
            }

            float previousValue = integerControl.ReadValueFromPreviousFrame();
            float value = integerControl.ReadValue();

            return Math.Abs(value - previousValue) >= RuntimeIntegerBinding.INTEGER_DELTA_THRESHOLD;
        }
    }
}
