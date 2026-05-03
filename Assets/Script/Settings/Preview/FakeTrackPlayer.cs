using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine.Keys;
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

            public bool UseKickFrets;
            public bool UseProKeys;

            public Dictionary<int, int> HighwayOrdering;
            public int LaneCount;
            #nullable enable
            public GameObject? FretPrefab;
            public GameObject? KickFretPrefab;
            #nullable restore

            public FretColorProviderFunc FretColorProvider;
            public NoteColorProviderFunc NoteColorProvider;

            public HitWindowProviderFunc HitWindowProvider;

            public CreateFakeNoteFunc CreateFakeNote;
        }

        private static readonly Dictionary<GameMode, Info> _gameModeInfos = new()
        {

        public GameMode CurrentGameMode { get; set; }
            {
                GameMode.FiveFretGuitar,
                new Info
                {
                    HighwayOrdering = FiveFretGuitarPlayer.DEFAULT_HIGHWAY_ORDERING,
                    LaneCount = 5,

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
                GameMode.SixFretGuitar,
                new Info
                {
                    HighwayOrdering = SixFretGuitarPlayer.DEFAULT_LANE_POSITIONS,
                    LaneCount = 3,

                    FretColorProvider = (colorProfile) => colorProfile.SixFretGuitar,
                    NoteColorProvider = (colorProfile, note) => colorProfile.SixFretGuitar
                        .GetNoteColor(note.Fret)
                        .ToUnityColor(),

                    HitWindowProvider = (enginePreset) => enginePreset.SixFretGuitar.HitWindow,

                    CreateFakeNote = (time) =>
                    {
                        // 0 = Open, 1-6 = 6 frets, 7 = Wildcard
                        int fret = Random.Range(0, 8);

                        // Open notes
                        if (fret == 0)
                        {
                            return new FakeNoteData
                            {
                                Time = time,
                                Fret = (int)SixFretGuitarFret.Open,
                                CenterNote = true,
                                NoteType = ThemeNoteType.Open
                            };
                        }

                        // Wildcard
                        if (fret == 7)
                        {
                            return new FakeNoteData
                            {
                                Time = time,
                                Fret = (int)SixFretGuitarFret.Wildcard,
                                CenterNote = true,
                                NoteType = ThemeNoteType.Wildcard
                            };
                        }

                        // Determine lane type for 3-lane preview
                        var fretEnum = (SixFretGuitarFret)fret;
                        bool isBlack = fret is >= 1 and <= 3; // Black1(1)-Black3(3)
                        bool isUp = isBlack; // Normal mode: black = up
                        bool isBarre = Random.value > 0.7f; // 30% chance of barre

                        // Map to theme note type
                        ThemeNoteType themeType = isBarre ? ThemeNoteType.SixFretBarre :
                            (isUp ? ThemeNoteType.SixFretUp : ThemeNoteType.SixFretDown);

                        // Override to HOPO/Tap variants randomly
                        var noteType = Random.Range(0, 3) switch
                        {
                            0 => themeType, // Strum
                            1 => isBarre ? themeType : // Barre only has strum
                                (isUp ? ThemeNoteType.SixFretUpHOPO : ThemeNoteType.SixFretDownHOPO),
                            2 => isBarre ? themeType : // Barre only has strum
                                (isUp ? ThemeNoteType.SixFretUpTap : ThemeNoteType.SixFretDownTap),
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
                    UseKickFrets = true,

                    HighwayOrdering = DrumsPlayer.DEFAULT_FOUR_LANE_HIGHWAY_ORDERING,
                    LaneCount = 4,

                    FretColorProvider = (colorProfile) => colorProfile.FourLaneDrums,
                    NoteColorProvider = (colorProfile, note) =>
                    {
                        int colorNote = (note.Fret, note.NoteType) switch
                        {
                            ((int) ColorProfile.FourLaneDrumsFret.Kick, _)                          => (int) ColorProfile.FourLaneDrumsFret.Kick,
                            ((int) ColorProfile.FourLaneDrumsFret.RedDrum, ThemeNoteType.Cymbal)    => (int) ColorProfile.FourLaneDrumsFret.RedCymbal,
                            ((int) ColorProfile.FourLaneDrumsFret.RedDrum, _)                       => (int) ColorProfile.FourLaneDrumsFret.RedDrum,
                            ((int) ColorProfile.FourLaneDrumsFret.YellowDrum, ThemeNoteType.Cymbal) => (int) ColorProfile.FourLaneDrumsFret.YellowCymbal,
                            ((int) ColorProfile.FourLaneDrumsFret.YellowDrum, _)                    => (int) ColorProfile.FourLaneDrumsFret.YellowDrum,
                            ((int) ColorProfile.FourLaneDrumsFret.BlueDrum, ThemeNoteType.Cymbal)   => (int) ColorProfile.FourLaneDrumsFret.BlueCymbal,
                            ((int) ColorProfile.FourLaneDrumsFret.BlueDrum, _)                      => (int) ColorProfile.FourLaneDrumsFret.BlueDrum,
                            ((int) ColorProfile.FourLaneDrumsFret.GreenDrum, ThemeNoteType.Cymbal)  => (int) ColorProfile.FourLaneDrumsFret.GreenCymbal,
                            ((int) ColorProfile.FourLaneDrumsFret.GreenDrum, _)                     => (int) ColorProfile.FourLaneDrumsFret.GreenDrum,
                            _                                                    => throw new Exception("Unreachable.")
                        };

                        return colorProfile.FourLaneDrums
                            .GetNoteColor(colorNote)
                            .ToUnityColor();
                    },

                    HitWindowProvider = (enginePreset) => enginePreset.Drums.HitWindow,

                    CreateFakeNote = (time) =>
                    {
                        int fret = Random.Range(0, 5);
                        ThemeNoteType noteType;

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

                        // First lane can't have cymbals
                        if (fret == 1)
                        {
                            noteType = ThemeNoteType.Normal;
                        }
                        else
                        {
                            noteType = Random.Range(0, 2) switch
                            {
                                0 => ThemeNoteType.Cymbal,
                                1 => ThemeNoteType.Normal,
                                _ => throw new Exception("Unreachable.")
                            };
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
                GameMode.FiveLaneDrums,
                new Info
                {
                    UseKickFrets = true,

                    FretColorProvider = (colorProfile) => colorProfile.FiveLaneDrums,
                    NoteColorProvider = (colorProfile, note) => colorProfile.FiveLaneDrums
                        .GetNoteColor(note.Fret)
                        .ToUnityColor(),

                    HighwayOrdering = DrumsPlayer.DEFAULT_FIVE_LANE_HIGHWAY_ORDERING,
                    LaneCount = 5,

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
                        if (fret is 2 or 4)
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

            // If we aren't using Pro Keys, then the passed instrument doesn't really matter; arbitrarily pass Five-Fret Guitar
            var style = VisualStyleHelpers.GetVisualStyle(SelectedGameMode, CurrentGameModeInfo.UseProKeys ? Instrument.ProKeys : Instrument.FiveFretGuitar);

            // Create frets and put then on the right layer
            if (!CurrentGameModeInfo.UseProKeys)
            {
                _fretArray.UseKickFrets = CurrentGameModeInfo.UseKickFrets;
                _fretArray.Initialize(
                    CurrentGameModeInfo.HighwayOrdering,
                    CurrentGameModeInfo.LaneCount,
                    CurrentGameModeInfo.KickFretPrefab,
                    CurrentGameModeInfo.FretColorProvider(ColorProfile.Default),
                    theme,
                    style
                );
                _fretArray.transform.SetLayerRecursive(LayerMask.NameToLayer("Settings Preview"));
            }

            // Create the note prefab (this has to be specially done, because
            // TrackElements need references to the GameManager)
            var prefab = FakeNote.CreateFakeNoteFromTheme(theme, style);
            prefab.transform.parent = transform;
            prefab.SetActive(false);
            _notePool.SetPrefabAndReset(prefab);

            // Show hit window if enabled
            _hitWindow.gameObject.SetActive(SettingsManager.Settings.ShowHitWindow.Value || ForceShowHitWindow);
            _hitWindow.NoteSpeed = NOTE_SPEED;
            _trackMaterial.StarpowerMode = ForceStarPower;
            _trackMaterial.GrooveMode = ForceGroove;

            SettingsMenu.Instance.SettingChanged += OnSettingChanged;

            var highwayRenderer = _cameraPositioner.GetComponent<HighwayCameraRendering>();
            var camera = _cameraPositioner.GetComponent<Camera>();
            highwayRenderer.AddPlayerParams(transform.position, camera, 0, 0, 0, 0, false);

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
            _cameraPositioner.Initialize(cameraPreset);

            var camera = _cameraPositioner.GetComponent<Camera>();
            var highwayRenderer = camera.GetComponent<HighwayCameraRendering>();
            highwayRenderer.UpdateCurveFactor(cameraPreset.CurveFactor, 0);
            highwayRenderer.UpdateFadeParams(0, 3f, cameraPreset.FadeLength);
            highwayRenderer.UpdateCameraProjectionMatrices();

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
