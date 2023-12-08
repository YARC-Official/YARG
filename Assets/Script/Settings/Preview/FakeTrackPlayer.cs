using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Gameplay;
using YARG.Gameplay.Player;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;
using YARG.Themes;
using Random = UnityEngine.Random;

namespace YARG.Settings.Preview
{
    public class FakeTrackPlayer : MonoBehaviour
    {
        public struct Info
        {
            public delegate ColorProfile.IFretColorProvider FretColorProviderFunc(ColorProfile c);
            public delegate FakeNoteData CreateFakeNoteFunc(double time);

            public int FretCount;
            public FretColorProviderFunc FretColorProviderGetter;
            public CreateFakeNoteFunc CreateFakeNote;
        }

        private static readonly Dictionary<GameMode, Info> _gameModeInfos = new()
        {
            {
                GameMode.FiveFretGuitar,
                new Info
                {
                    FretCount = 5,
                    FretColorProviderGetter = (colorProfile) => colorProfile.FiveFretGuitar,
                    CreateFakeNote = (time) =>
                    {
                        int fret = Random.Range(0, 6);

                        // Open notes have different models
                        if (fret == 0)
                        {
                            return new FakeNoteData
                            {
                                Time = time,

                                Fret = fret,
                                CenterNote = true,
                                NoteType = ThemeNoteType.Open
                            };
                        }

                        // Otherwise, select a random note type
                        var noteType = Random.Range(0, 3) switch
                        {
                            0 => ThemeNoteType.Normal,
                            1 => ThemeNoteType.HOPO,
                            2 => ThemeNoteType.Tap,
                            _ => throw new Exception("Unreachable.")
                        };

                        return new FakeNoteData
                        {
                            Time = time,

                            Fret = fret,
                            CenterNote = false,
                            NoteType = noteType
                        };
                    }
                }
            }
        };

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
        private FakeHitWindowDisplay _hitWindow;

        public bool ForceShowHitWindow { get; set; }

        public double PreviewTime { get; private set; }
        private double _nextSpawnTime;

        // TODO: Make game mode selectable
        public Info CurrentGameModeInfo { get; private set; }
        private GameMode _selectedGameMode = GameMode.FiveFretGuitar;

        private void Start()
        {
            CurrentGameModeInfo = _gameModeInfos[_selectedGameMode];
            var theme = ThemePreset.Default;

            // Create frets and put then on the right layer
            var fret = ThemeManager.Instance.CreateFretPrefabFromTheme(theme, _selectedGameMode);
            _fretArray.FretCount = CurrentGameModeInfo.FretCount;
            _fretArray.Initialize(fret,
                CurrentGameModeInfo.FretColorProviderGetter(ColorProfile.Default), false);
            _fretArray.transform.SetLayerRecursive(LayerMask.NameToLayer("Settings Preview"));

            // Create the note prefab (this has to be specially done, because
            // TrackElements need references to the GameManager)
            var prefab = FakeNote.CreateFakeNoteFromTheme(theme, _selectedGameMode);
            prefab.transform.parent = transform;
            prefab.SetActive(false);
            _notePool.SetPrefabAndReset(prefab);

            // Show hit window if enabled
            _hitWindow.gameObject.SetActive(SettingsManager.Settings.ShowHitWindow.Value || ForceShowHitWindow);
            _hitWindow.NoteSpeed = NOTE_SPEED;

            SettingsMenu.Instance.SettingChanged += OnSettingChanged;

            // Force update it as well to make sure it's right before any settings are changed
            OnSettingChanged();
        }

        private void OnSettingChanged()
        {
            var cameraPreset = PresetsTab.GetLastSelectedPreset(CustomContentManager.CameraSettings);
            var colorProfile = PresetsTab.GetLastSelectedPreset(CustomContentManager.ColorProfiles);
            var enginePreset = PresetsTab.GetLastSelectedPreset(CustomContentManager.EnginePresets);

            // Update camera presets
            _trackMaterial.Initialize(3f, cameraPreset.FadeLength);
            _cameraPositioner.Initialize(cameraPreset);

            // Update color profiles
            _fretArray.InitializeColor(colorProfile.FiveFretGuitar);

            // Update hit window
            _hitWindow.HitWindow = enginePreset.FiveFretGuitar.HitWindow.Create();

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
                var note = CurrentGameModeInfo.CreateFakeNote(PreviewTime + SpawnTimeOffset);

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