using System;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Input;
using YARG.Settings.Customization;

namespace YARG.Player
{
    public class YargPlayer : IDisposable
    {
        public event MenuInputEvent MenuInput;

        public YargProfile Profile { get; private set; }

        // public MicInput MicInput;

        public bool InputsEnabled { get; private set; } = false;
        public ProfileBindings Bindings { get; private set; }

        public ColorProfile ColorProfile = ColorProfile.Default;
        public CameraSettings CameraSettings = CameraSettings.Default;

        public YargPlayer(YargProfile profile, ProfileBindings bindings)
        {
            SwapToProfile(profile, bindings);
        }

        public void SwapToProfile(YargProfile profile, ProfileBindings bindings)
        {
            // Force-disable inputs
            bool enabled = InputsEnabled;
            DisableInputs();

            // Swap to the new profile
            Bindings?.Dispose();
            Profile = profile;
            Bindings = bindings;

            // Resolve bindings
            Bindings.ResolveDevices();

            // Re-enable inputs
            if (enabled)
                EnableInputs();
        }

        public void EnableInputs()
        {
            if (InputsEnabled || Bindings == null)
                return;

            Bindings.EnableInputs();
            Bindings.MenuInputProcessed += OnMenuInput;
            InputManager.RegisterPlayer(this);

            InputsEnabled = true;
        }

        public void DisableInputs()
        {
            if (!InputsEnabled || Bindings == null)
                return;

            Bindings.DisableInputs();
            Bindings.MenuInputProcessed -= OnMenuInput;
            InputManager.UnregisterPlayer(this);

            InputsEnabled = false;
        }

        private void OnMenuInput(ref GameInput input)
        {
            MenuInput?.Invoke(this, ref input);
        }

        public void Dispose()
        {
            DisableInputs();
            Bindings?.Dispose();
        }
    }
}