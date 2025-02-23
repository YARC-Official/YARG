using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine.ProKeys;
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
            public delegate Color NoteColorProviderFunc(ColorProfile c, FakeNoteData note);
            public delegate EnginePreset.HitWindowPreset HitWindowProviderFunc(EnginePreset e);
            public delegate FakeNoteData CreateFakeNoteFunc(double time);

            public int FretCount;
            public bool UseKickFrets;
            public bool UseProKeys;

            public FretColorProviderFunc FretColorProvider;
            public NoteColorProviderFunc NoteColorProvider;

            public HitWindowProviderFunc HitWindowProvider;

            public CreateFakeNoteFunc CreateFakeNote;
        }

        private static readonly Dictionary<GameMode, Info> _gameModeInfos = new()
        {
            {
                GameMode.FiveFretGuitar,
                new Info
                {
                    FretCount = 5,

                    FretColorProvider = (colorProfile) => colorProfile.FiveFretGuitar,
                    NoteColorProvider = (colorProfile, note) => colorProfile.FiveFretGuitar
                        .GetNoteColor(note.Fret)
                        .ToUnityColor(),

                    HitWindowProvider = (enginePreset) => enginePreset.FiveFretGuitar.HitWindow,

                    CreateFakeNote = (time) =>
                    {
                        // Here we use 0 as open as it's easier to visualize.
                        // We convert this into the correct value in the if below.
                        int fret = Random.Range(0, 6);

                        // Open notes have different models
                        if (fret == 0)
                        {
                            return new FakeNoteData
                            {
                                Time = time,

                                Fret = (int) FiveFretGuitarFret.Open,
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
            },
            {
                GameMode.FourLaneDrums,
                new Info
                {
                    FretCount = 4,
                    UseKickFrets = true,

                    FretColorProvider = (colorProfile) => colorProfile.FourLaneDrums,
                    NoteColorProvider = (colorProfile, note) =>
                    {
                        int colorNote = (note.Fret, note.NoteType) switch
                        {
                            (0, _) => 0, // Kick
                            (1, ThemeNoteType.Cymbal) => 8, // The forbidden red cymbal
                            (1, _) => 1, // Red drum
                            (2, ThemeNoteType.Cymbal) => 5, // Yellow cymbal
                            (2, _) => 2, // Yellow drum
                            (3, ThemeNoteType.Cymbal) => 6, // Blue cymbal
                            (3, _) => 3, // Blue drum
                            (4, ThemeNoteType.Cymbal) => 7, // Green cymbal
                            (4, _) => 4, // Green drum
                            _ => throw new Exception("Unreachable.")
                        };

                        return colorProfile.FourLaneDrums
                            .GetNoteColor(colorNote)
                            .ToUnityColor();
                    },

                    HitWindowProvider = (enginePreset) => enginePreset.Drums.HitWindow,

                    CreateFakeNote = (time) =>
                    {
                        int fret = Random.Range(0, 5);

                        // Kick notes have different models
                        if (fret == 0)
                        {
                            return new FakeNoteData
                            {
                                Time = time,

                                Fret = fret,
                                CenterNote = true,
                                NoteType = ThemeNoteType.Kick
                            };
                        }

                        // Otherwise, select a random note type
                        var noteType = Random.Range(0, 2) switch
                        {
                            0 => ThemeNoteType.Normal,
                            1 => ThemeNoteType.Cymbal,
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
            },
            {
                GameMode.FiveLaneDrums,
                new Info
                {
                    FretCount = 5,
                    UseKickFrets = true,

                    FretColorProvider = (colorProfile) => colorProfile.FiveLaneDrums,
                    NoteColorProvider = (colorProfile, note) => colorProfile.FiveLaneDrums
                        .GetNoteColor(note.Fret)
                        .ToUnityColor(),

                    HitWindowProvider = (enginePreset) => enginePreset.Drums.HitWindow,

                    CreateFakeNote = (time) =>
                    {
                        int fret = Random.Range(0, 6);

                        // Kick notes have different models
                        if (fret == 0)
                        {
                            return new FakeNoteData
                            {
                                Time = time,

                                Fret = fret,
                                CenterNote = true,
                                NoteType = ThemeNoteType.Kick
                            };
                        }

                        // Otherwise, select the correct note type
                        var noteType = ThemeNoteType.Normal;
                        if (SettingsManager.Settings.UseCymbalModelsInFiveLane.Value && fret is 2 or 4)
                        {
                            noteType = ThemeNoteType.Cymbal;
                        }

                        return new FakeNoteData
                        {
                            Time = time,

                            Fret = fret,
                            CenterNote = false,
                            NoteType = noteType
                        };
                    }
                }
            },
            {
                GameMode.ProKeys,
                new Info
                {
                    UseProKeys = true,

                    FretColorProvider = null,
                    NoteColorProvider = (colorProfile, note) => (ProKeysUtilities.IsWhiteKey(note.Fret % 12)
                        ? colorProfile.ProKeys.WhiteNote
                        : colorProfile.ProKeys.BlackNote).ToUnityColor(),

                    HitWindowProvider = (enginePreset) => enginePreset.ProKeys.HitWindow,

                    CreateFakeNote = (time) =>
                    {
                        int fret = Random.Range(0, 17);

                        // Otherwise, select the correct note type
                        var noteType = ThemeNoteType.White;
                        if (ProKeysUtilities.IsBlackKey(fret % 12))
                        {
                            noteType = ThemeNoteType.Black;
                        }

                        return new FakeNoteData
                        {
                            Time = time,

                            Fret = fret,
                            CenterNote = true,
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
        public bool ForceGroove { get; set; }
        public bool ForceStarPower { get; set; }

        public GameMode SelectedGameMode { get; set; } = GameMode.FiveFretGuitar;

        public double PreviewTime { get; private set; }
        private double _nextSpawnTime;

        public Info CurrentGameModeInfo { get; private set; }

        private void Start()
        {
            CurrentGameModeInfo = _gameModeInfos[SelectedGameMode];
            var theme = ThemePreset.Default;

            // Create frets and put then on the right layer
            if (!CurrentGameModeInfo.UseProKeys)
            {
                _fretArray.FretCount = CurrentGameModeInfo.FretCount;
                _fretArray.UseKickFrets = CurrentGameModeInfo.UseKickFrets;
                _fretArray.Initialize(theme, SelectedGameMode,
                    CurrentGameModeInfo.FretColorProvider(ColorProfile.Default), false);
                _fretArray.transform.SetLayerRecursive(LayerMask.NameToLayer("Settings Preview"));
            }

            // Create the note prefab (this has to be specially done, because
            // TrackElements need references to the GameManager)
            var prefab = FakeNote.CreateFakeNoteFromTheme(theme, SelectedGameMode);
            prefab.transform.parent = transform;
            prefab.SetActive(false);
            _notePool.SetPrefabAndReset(prefab);

            // Show hit window if enabled
            _hitWindow.gameObject.SetActive(SettingsManager.Settings.ShowHitWindow.Value || ForceShowHitWindow);
            _hitWindow.NoteSpeed = NOTE_SPEED;
            _trackMaterial.StarpowerMode = ForceStarPower;
            _trackMaterial.GrooveMode = ForceGroove;

            SettingsMenu.Instance.SettingChanged += OnSettingChanged;

            // Force update it as well to make sure it's right before any settings are changed
            OnSettingChanged();
        }

        private void OnSettingChanged()
        {
            var cameraPreset = PresetsTab.GetLastSelectedPreset(CustomContentManager.CameraSettings);
            var colorProfile = PresetsTab.GetLastSelectedPreset(CustomContentManager.ColorProfiles);
            var enginePreset = PresetsTab.GetLastSelectedPreset(CustomContentManager.EnginePresets);
            var highwayPreset = PresetsTab.GetLastSelectedPreset(CustomContentManager.HighwayPresets);

            // Update camera presets
            _trackMaterial.Initialize(highwayPreset);
            _cameraPositioner.Initialize(cameraPreset, 3f, cameraPreset.FadeLength);

            // A bit ugly but this is only in settings.
            var curveHandler = FindFirstObjectByType<TrackCurveHandler>();
            var camera = _cameraPositioner.GetComponent<Camera>();
            var fadeParams = YARG.Gameplay.Visuals.Utils.FadeSettingsToScreenParams(
                                                this.transform.position,
                                                camera.activeTexture.height,
                                                camera,
                                                3f, cameraPreset.FadeLength);
            curveHandler.FadeParams = fadeParams;

            // Update color profiles
            if (!CurrentGameModeInfo.UseProKeys)
            {
                _fretArray.InitializeColor(CurrentGameModeInfo.FretColorProvider(colorProfile), false);
            }

            // Update hit window
            _hitWindow.HitWindow = CurrentGameModeInfo.HitWindowProvider(enginePreset).Create();

            // Update all of the notes
            foreach (var note in _notePool.AllSpawned)
            {
                ((FakeNote)note).OnSettingChanged();
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
                var noteObj = (FakeNote)_notePool.KeyedTakeWithoutEnabling(note);
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
