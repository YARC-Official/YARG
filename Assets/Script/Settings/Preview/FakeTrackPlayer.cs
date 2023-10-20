﻿using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Menu.Settings;
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
            _fretArray.Initialize(ColorProfile.Default.FiveFretGuitar, false);
            _hitWindow.gameObject.SetActive(SettingsManager.Settings.ShowHitWindow.Data);

            SettingsMenu.Instance.SettingChanged += OnSettingChanged;

            // Force update it as well to make sure it's right before any settings are changed
            OnSettingChanged();
        }

        private void OnSettingChanged()
        {
            var s = SettingsManager.Settings;

            // Update camera presets
            _trackMaterial.Initialize(3f, s.CameraPreset_FadeLength.Data);
            _cameraPositioner.Initialize(
                s.CameraPreset_FieldOfView.Data,
                s.CameraPreset_PositionY.Data,
                s.CameraPreset_PositionZ.Data,
                s.CameraPreset_Rotation.Data);

            // Update color profiles
            _fretArray.InitializeColor(s.ColorProfile_Ref.FiveFretGuitar);
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