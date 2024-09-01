using System;
using YARG.Core.Engine;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Input;
using YARG.Settings.Customization;
using YARG.Themes;

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

        public bool InputsEnabled { get; private set; }
        public ProfileBindings Bindings { get; private set; }

        public EnginePreset EnginePreset { get; private set; }
        public ThemePreset  ThemePreset  { get; private set; }
        public ColorProfile ColorProfile { get; private set; }
        public CameraPreset CameraPreset { get; private set; }

        /// <summary>
        /// Overrides the engine parameters in the gameplay player.
        /// This is only used when loading replays.
        /// </summary>
        public BaseEngineParameters EngineParameterOverride { get; set; }

        public YargPlayer(YargProfile profile, ProfileBindings bindings, bool resolveDevices)
        {
            SwapToProfile(profile, bindings, resolveDevices);
            SetPresetsFromProfile();
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
            {
                EnableInputs();
            }
        }

        public void SetPresetsFromProfile()
        {
            EnginePreset = CustomContentManager.EnginePresets.GetPresetById(Profile.EnginePreset)
                ?? EnginePreset.Default;
            ThemePreset = CustomContentManager.ThemePresets.GetPresetById(Profile.ThemePreset)
                ?? ThemePreset.Default;
            ColorProfile = CustomContentManager.ColorProfiles.GetPresetById(Profile.ColorProfile)
                ?? ColorProfile.Default;
            CameraPreset = CustomContentManager.CameraSettings.GetPresetById(Profile.CameraPreset)
                ?? CameraPreset.Default;
        }

        public void SetPresetsFromReplay(ReplayData replay)
        {
            var colorProfile = replay.GetColorProfile(Profile.ColorProfile);
            if (colorProfile is not null)
            {
                ColorProfile = colorProfile;
            }

            var cameraPreset = replay.GetCameraPreset(Profile.CameraPreset);
            if (cameraPreset is not null)
            {
                CameraPreset = cameraPreset;
            }
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