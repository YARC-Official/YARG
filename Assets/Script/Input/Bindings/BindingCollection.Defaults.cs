using System;
using PlasticBand.Devices;
using UnityEngine.InputSystem;

namespace YARG.Input
{
    public partial class BindingCollection
    {
        // Return is a dummy so we can use switch expressions
        public bool SetDefaultBindings(InputDevice device)
        {
            // Get the real device type
            return device switch
            {
                Keyboard keyboard => SetDefaultBindings(keyboard),
                Gamepad gamepad => SetDefaultBindings(gamepad, GamepadBindingMode.Gamepad),

                FiveFretGuitar guitar => SetDefaultBindings(guitar),
                SixFretGuitar guitar => SetDefaultBindings(guitar),

                FourLaneDrumkit drums => SetDefaultBindings(drums),
                FiveLaneDrumkit drums => SetDefaultBindings(drums),

                ProGuitar guitar => SetDefaultBindings(guitar),
                ProKeyboard keyboard => SetDefaultBindings(keyboard),

                // Turntable turntable => SetDefaultBindings(turntable),

                _ => false
            };
        }

        public bool SetDefaultBindings(Keyboard keyboard)
        {
            return IsMenu ? SetDefaultMenuBindings(keyboard) : SetDefaultGameplayBindings(keyboard);
        }

        public bool SetDefaultBindings(Gamepad gamepad, GamepadBindingMode mode)
        {
            return IsMenu ? SetDefaultMenuBindings(gamepad, mode) : SetDefaultGameplayBindings(gamepad, mode);
        }

        public bool SetDefaultBindings(FiveFretGuitar guitar)
        {
            return IsMenu ? SetDefaultMenuBindings(guitar) : SetDefaultGameplayBindings(guitar);
        }

        public bool SetDefaultBindings(SixFretGuitar guitar)
        {
            return IsMenu ? SetDefaultMenuBindings(guitar) : SetDefaultGameplayBindings(guitar);
        }

        public bool SetDefaultBindings(FourLaneDrumkit drums)
        {
            return IsMenu ? SetDefaultMenuBindings(drums) : SetDefaultGameplayBindings(drums);
        }

        public bool SetDefaultBindings(FiveLaneDrumkit drums)
        {
            return IsMenu ? SetDefaultMenuBindings(drums) : SetDefaultGameplayBindings(drums);
        }

        public bool SetDefaultBindings(ProGuitar guitar)
        {
            return IsMenu ? SetDefaultMenuBindings(guitar) : SetDefaultGameplayBindings(guitar);
        }

        public bool SetDefaultBindings(ProKeyboard keyboard)
        {
            return IsMenu ? SetDefaultMenuBindings(keyboard) : SetDefaultGameplayBindings(keyboard);
        }

        private void AddBinding<TAction>(TAction action, InputControl control)
            where TAction : unmanaged, Enum
        {
            AddBinding(action, control, ActuationSettings.Default);
        }

        private void AddBinding<TAction>(TAction action, InputControl control, ActuationSettings settings)
            where TAction : unmanaged, Enum
        {
            var binding = TryGetBindingByAction(action);
            if (binding is null)
                throw new Exception($"Tried to auto-assign control {control} to action {action}, but no binding exists for it!");

            // Ignore if the control is already added, otherwise removing a device without
            // clearing any bindings to it will throw below
            if (binding.ContainsControl(control))
                return;

            bool added = binding.AddControl(settings, control);
            if (!added)
                throw new Exception($"Could not auto-assign control {control} (type {control.GetType()}) to action {action}!");
        }

    }
}