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

        // public InputStrategy InputStrategy;
        // public MicInput MicInput;

        public bool InputsEnabled { get; private set; }
        public ProfileBindings Bindings { get; private set; }

        public ColorProfile ColorProfile = ColorProfile.Default;
        public CameraSettings CameraSettings = CameraSettings.Default;

        public YargPlayer(YargProfile profile)
        {
            SwapToProfile(profile);
        }

        public void SwapToProfile(YargProfile profile)
        {
            // TODO: deal with the previous bindings, etc.

            // Force-disable inputs
            DisableInputs();

            Profile = profile;
            Bindings = new(profile);
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
            // InputStrategy?.Dispose();
        }
    }
}