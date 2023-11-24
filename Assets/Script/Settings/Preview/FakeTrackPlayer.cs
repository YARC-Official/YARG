using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;
using YARG.Themes;
using Random = UnityEngine.Random;

namespace YARG.Settings.Preview
{
    public class FakeTrackPlayer : MonoBehaviour
    {
        public const float NOTE_SPEED = 6f;
        private const double SPAWN_FREQ = 0.2;

        private double SpawnTimeOffset => (TrackPlayer.NOTE_SPAWN_OFFSET + -TrackPlayer.STRIKE_LINE_POS) / NOTE_SPEED;

        [SerializeField]
        private CameraPositioner _cameraPositioner;
        [SerializeField]
        private TrackMaterial _trackMaterial;
        [SerializeField]
        private FretArray _fretArray;
        [SerializeField]
        private KeyedPool _notePool;
        [SerializeField]
        private GameObject _hitWindow;

        public double PreviewTime { get; private set; }
        private double _nextSpawnTime;

        private void Start()
        {
            // TODO: Redo
            var fret = ThemeManager.Instance.CreateFretPrefabFromTheme(ThemePreset.Default, GameMode.FiveFretGuitar);

            _fretArray.Initialize(fret, ColorProfile.Default.FiveFretGuitar, false);
            _hitWindow.gameObject.SetActive(SettingsManager.Settings.ShowHitWindow.Data);

            SettingsMenu.Instance.SettingChanged += OnSettingChanged;

            // Force update it as well to make sure it's right before any settings are changed
            OnSettingChanged();
        }

        private void OnSettingChanged()
        {
            var cameraPreset = PresetsTab.GetLastSelectedPreset(CustomContentManager.CameraSettings);
            var colorProfile = PresetsTab.GetLastSelectedPreset(CustomContentManager.ColorProfiles);

            // Update camera presets
            _trackMaterial.Initialize(3f, cameraPreset.FadeLength);
            _cameraPositioner.Initialize(cameraPreset);

            // Update color profiles
            _fretArray.InitializeColor(colorProfile.FiveFretGuitar);

            // Update all of the notes
            foreach (var note in _notePool.AllSpawned)
            {
                ((FakeNote) note).OnSettingChanged();
            }
        }

        private void Update()
        {
            // Update the preview notes
            PreviewTime += Time.deltaTime;

            // Queue the notes
            if (_nextSpawnTime <= PreviewTime)
            {
                // Create a fake note. Ticks do not matter.
                var note = new GuitarNote(
                    Random.Range(0, 6),
                    GuitarNoteType.Strum,
                    GuitarNoteFlags.None,
                    NoteFlags.None,
                    PreviewTime + SpawnTimeOffset,
                    0, 0, 0);

                // Create note every N seconds
                _nextSpawnTime = PreviewTime + SPAWN_FREQ;

                // Spawn note
                var noteObj = (FakeNote) _notePool.KeyedTakeWithoutEnabling(note);
                noteObj.NoteRef = note;
                noteObj.FakeTrackPlayer = this;
                noteObj.EnableFromPool();
            }

            _trackMaterial.SetTrackScroll(PreviewTime, NOTE_SPEED);
        }

        private void OnDestroy()
        {
            SettingsMenu.Instance.SettingChanged -= OnSettingChanged;
        }
    }
}