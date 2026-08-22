using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Settings;
using YARG.Settings.Preview;
using YARG.Themes;

// pattern: Imperative Shell

namespace YARG.Settings.Metadata
{
    public class TrackPreviewBuilder : IPreviewBuilder
    {
        // Prefabs needed for this tab type
        private static GameObject _trackPreview;
        private static GameObject _trackPreviewUI;

        public GameMode? StartingGameMode { get; set; }

        private readonly bool _forceShowHitWindow;

        private bool _forceGroove;
        public bool ForceGroove
        {
            get => _forceGroove;
            set
            {
                _forceGroove = value;
                if (_currentTrackPreview != null)
                {
                    _currentTrackPreview.ForceGroove = value;
                }
            }
        }

        private bool _forceStarPower;
        public bool ForceStarPower
        {
            get => _forceStarPower;
            set
            {
                _forceStarPower = value;
                if (_currentTrackPreview != null)
                {
                    _currentTrackPreview.ForceStarPower = value;
                }
            }
        }

        private FakeTrackPlayer _currentTrackPreview;
        private bool _forceStarPowerNotes;
        public bool ForceStarPowerNotes
        {
            get => _forceStarPowerNotes;
            set
            {
                _forceStarPowerNotes = value;

                // Propagate to the live player so existing notes recolor without a
                // rebuild. The auto-fired SettingsMenu.OnSettingChanged() drives the
                // recolor via the SettingChanged event. Use Unity's overloaded !=
                // (not `is not null`) so a destroyed/old player is treated as null.
                if (_currentTrackPreview != null)
                {
                    _currentTrackPreview.ForceStarPowerNotes = value;
                }
            }
        }

        private bool _leftyFlip;
        public bool LeftyFlip
        {
            get => _leftyFlip;
            set
            {
                _leftyFlip = value;

                // Propagate to the live player (mirrors ForceStarPowerNotes).
                if (_currentTrackPreview != null)
                {
                    _currentTrackPreview.LeftyFlip = value;
                }
            }
        }

        /// <summary>
        /// Forwards a lane spotlight request to the live preview (see
        /// <see cref="Preview.FakeTrackPlayer.SpotlightLane"/>). No-op when no
        /// preview is currently alive.
        /// </summary>
        public void SpotlightLane(int fret, bool centerNote, bool cymbal, bool starPower)
        {
            if (_currentTrackPreview != null)
            {
                _currentTrackPreview.SpotlightLane(fret, centerNote, cymbal, starPower);
            }
        }

        /// <summary>
        /// Forwards to <see cref="FakeTrackPlayer.SpotlightNoteType"/>.
        /// No-op when no preview is currently alive.
        /// </summary>
        public void SpotlightNoteType(ThemeNoteType noteType, bool? starPower = null)
        {
            if (_currentTrackPreview != null)
            {
                _currentTrackPreview.SpotlightNoteType(noteType, starPower);
            }
        }

        /// <summary>
        /// Forwards a Pro Keys white/black note spotlight to the live preview.
        /// </summary>
        public void SpotlightProKeysNoteType(bool black, bool starPower)
        {
            if (_currentTrackPreview != null)
            {
                _currentTrackPreview.SpotlightProKeysNoteType(black, starPower);
            }
        }

        /// <summary>
        /// Forwards to <see cref="FakeTrackPlayer.SpotlightMiss"/>.
        /// </summary>
        public void SpotlightMiss()
        {
            if (_currentTrackPreview != null)
            {
                _currentTrackPreview.SpotlightMiss();
            }
        }

        /// <summary>
        /// Forwards to <see cref="FakeTrackPlayer.SpotlightStarPower"/>.
        /// </summary>
        public void SpotlightStarPower()
        {
            if (_currentTrackPreview != null)
            {
                _currentTrackPreview.SpotlightStarPower();
            }
        }

        public TrackPreviewBuilder(bool forceShowHitWindow = false, bool forceGroove = false, bool forceStarPower = false)
        {
            _forceShowHitWindow = forceShowHitWindow;
            _forceGroove = forceGroove;
            ForceStarPower = forceStarPower;
        }

        public UniTask BuildPreviewWorld(Transform worldContainer)
        {
            if (_trackPreview == null)
            {
                 _trackPreview = Addressables
                    .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreview")
                    .WaitForCompletion();
            }
            var trackObj = Object.Instantiate(_trackPreview, worldContainer);
            var trackPreview = trackObj.GetComponentInChildren<FakeTrackPlayer>();
            _currentTrackPreview = trackPreview;

            trackPreview.ForceShowHitWindow = _forceShowHitWindow;
            trackPreview.ForceGroove = _forceGroove;
            trackPreview.ForceStarPower = ForceStarPower;
            trackPreview.ForceStarPowerNotes = ForceStarPowerNotes;
            trackPreview.LeftyFlip = LeftyFlip;

            // If null, just use the default value and skip setting it
            if (StartingGameMode is not null)
            {
                trackPreview.SelectedGameMode = StartingGameMode.Value;
            }

            return UniTask.CompletedTask;
        }

        public async UniTask BuildPreviewUI(Transform uiContainer)
        {
            if (_trackPreviewUI == null)
            {
                _trackPreviewUI = Addressables
                    .LoadAssetAsync<GameObject>("SettingPreviews/TrackPreviewUI")
                    .WaitForCompletion();
            }
            var go = Object.Instantiate(_trackPreviewUI, uiContainer);

            // Enable and wait for layouts to rebuild
            await UniTask.WaitForEndOfFrame(SettingsMenu.Instance);

            // Skip the game object was somehow destroyed
            if (go == null) return;

            // Show the raw image
            var previewTexture = go.GetComponentInChildren<RawImage>();
            previewTexture.texture = CameraPreviewTexture.PreviewTexture;
            previewTexture.color = Color.white;

            // Size raw image
            var rect = previewTexture.rectTransform.ToViewportSpaceCentered(v: false, scale: 0.9f);
            rect.y = 0f;
            previewTexture.uvRect = rect;
        }
    }
}
