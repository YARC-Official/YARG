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

        /// <summary>
        /// Whether or not the player is sitting out. This is not needed in <see cref="Profile"/> as
        /// players that are sitting out are not included in replays.
        /// </summary>
        public bool SittingOut;

        public bool InputsEnabled { get; private set; } = false;
        public ProfileBindings Bindings { get; private set; }

        // This is done so that we can override the color profile for the player for replays
        public ColorProfile ColorProfile
        {
            get
            {
                if (_isOverrideColorProfile)
                {
                    return _colorProfile;
                }

                return CustomContentManager.ColorProfiles.GetPresetById(Profile.ColorProfile)
                    ?? ColorProfile.Default;
            }
        }

        public CameraPreset CameraPreset =>
            CustomContentManager.CameraSettings.GetPresetById(Profile.CameraPreset)
            ?? CameraPreset.Default;

        private bool _isOverrideColorProfile;
        private ColorProfile _colorProfile;

        public YargPlayer(YargProfile profile, ProfileBindings bindings, bool resolveDevices)
        {
            SwapToProfile(profile, bindings, resolveDevices);
        }

        public void SwapToProfile(YargProfile profile, ProfileBindings bindings, bool resolveDevices)
        {
            // Force-disable inputs
            bool enabled = InputsEnabled;
            DisableInputs();

            // Swap to the new profile
            Bindings?.Dispose();
            Profile = profile;
            Bindings = bindings;

            // Resolve bindings
            if (resolveDevices)
            {
                Bindings?.ResolveDevices();
            }

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

        public void OverrideColorProfile(ColorProfile profile)
        {
            _isOverrideColorProfile = true;
            _colorProfile = profile;
        }

        public void ResetColorProfile()
        {
            _isOverrideColorProfile = false;
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