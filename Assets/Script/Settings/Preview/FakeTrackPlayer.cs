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
        public abstract class Info
        {
            public delegate Color NoteColorProviderFunc(ColorProfile c, FakeNoteData note);
            public delegate EnginePreset.HitWindowPreset HitWindowProviderFunc(EnginePreset e);
            public delegate FakeNoteData CreateFakeNoteFunc(double time);

            public readonly int FretCount;
            public readonly bool UseKickFrets;

            public readonly NoteColorProviderFunc NoteColorProvider;
            public readonly HitWindowProviderFunc HitWindowProvider;
            public readonly CreateFakeNoteFunc CreateFakeNote;

            protected Info(int fretCount, bool useKickFrets, NoteColorProviderFunc noteColorProvider, HitWindowProviderFunc hitWindowProvider, CreateFakeNoteFunc createFakeNote)
            {
                FretCount = fretCount;
                UseKickFrets = useKickFrets;
                NoteColorProvider = noteColorProvider;
                HitWindowProvider = hitWindowProvider;
                CreateFakeNote = createFakeNote;
            }

            public abstract void Init(FretArray array, ThemePreset themePreset, GameMode gameMode);
            public abstract void InitColor(FretArray array, ColorProfile profile);
        }

        public class FretColorInfo<TProvider> : Info
            where TProvider : struct, ColorProfile.IFretColorProvider
        {
            public delegate ref readonly TProvider FretColorProviderFunc(ColorProfile c);
            public readonly FretColorProviderFunc FretColorProvider;

            public FretColorInfo(int fretCount, bool useKickFrets, FretColorProviderFunc fretColorProvider, NoteColorProviderFunc noteColorProvider, HitWindowProviderFunc hitWindowProvider, CreateFakeNoteFunc createFakeNote)
                : base(fretCount, useKickFrets, noteColorProvider, hitWindowProvider, createFakeNote)
            {
                FretColorProvider = fretColorProvider;
            }

            public override void Init(FretArray array, ThemePreset themePreset, GameMode gameMode)
            {
                array.Initialize(themePreset, gameMode, in FretColorProvider(ColorProfile.Default), false);
            }

            public override void InitColor(FretArray array, ColorProfile profile)
            {
                array.InitializeColor(in FretColorProvider(profile), false);
            }
        }

        private static readonly Dictionary<GameMode, Info> _gameModeInfos = new()
        {
            {
                GameMode.FiveFretGuitar,
                new FretColorInfo<ColorProfile.FiveFretGuitarColors>(
                    fretCount: 5,
                    useKickFrets: false,
                    fretColorProvider: (colorProfile) => ref colorProfile.FiveFretGuitar,
                    noteColorProvider: (colorProfile, note) => colorProfile.FiveFretGuitar
                        .GetNoteColor(note.Fret)
                        .ToUnityColor(),
                    hitWindowProvider: (enginePreset) => enginePreset.FiveFretGuitar.HitWindow,
                    createFakeNote: (time) =>
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
                )
            },
            {
                GameMode.FourLaneDrums,
                new FretColorInfo<ColorProfile.FourLaneDrumsColors>(
                    fretCount: 4,
                    useKickFrets: true,
                    fretColorProvider: (colorProfile) => ref colorProfile.FourLaneDrums,
                    noteColorProvider: (colorProfile, note) =>
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
                    hitWindowProvider: (enginePreset) => enginePreset.Drums.HitWindow,
                    createFakeNote: (time) =>
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
                )
            },
            {
                GameMode.FiveLaneDrums,
                new FretColorInfo<ColorProfile.FiveLaneDrumsColors>(
                    fretCount: 5,
                    useKickFrets: true,
                    fretColorProvider: (colorProfile) => ref colorProfile.FiveLaneDrums,
                    noteColorProvider: (colorProfile, note) => colorProfile.FiveLaneDrums
                        .GetNoteColor(note.Fret)
                        .ToUnityColor(),
                    hitWindowProvider: (enginePreset) => enginePreset.Drums.HitWindow,
                    createFakeNote: (time) =>
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
                )
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
        public GameMode SelectedGameMode { get; set; } = GameMode.FiveFretGuitar;

        public double PreviewTime { get; private set; }
        private double _nextSpawnTime;

        public Info CurrentGameModeInfo { get; private set; }

        private void Start()
        {
            CurrentGameModeInfo = _gameModeInfos[SelectedGameMode];
            var theme = ThemePreset.Default;

            // Create frets and put then on the right layer
            _fretArray.FretCount = CurrentGameModeInfo.FretCount;
            _fretArray.UseKickFrets = CurrentGameModeInfo.UseKickFrets;
            CurrentGameModeInfo.Init(_fretArray, theme, SelectedGameMode);

            _fretArray.transform.SetLayerRecursive(LayerMask.NameToLayer("Settings Preview"));

            // Create the note prefab (this has to be specially done, because
            // TrackElements need references to the GameManager)
            var prefab = FakeNote.CreateFakeNoteFromTheme(theme, SelectedGameMode);
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
            CurrentGameModeInfo.InitColor(_fretArray, colorProfile);

            // Update hit window
            _hitWindow.HitWindow = CurrentGameModeInfo.HitWindowProvider(enginePreset).Create();

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