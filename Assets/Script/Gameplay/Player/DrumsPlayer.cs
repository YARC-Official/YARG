using System;
using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Scores;

namespace YARG.Gameplay.Player
{
    public class DrumsPlayer : TrackPlayer<DrumsEngine, DrumNote>
    {
        public DrumsEngineParameters EngineParams { get; private set; }

        [Header("Drums Specific")]
        [SerializeField]
        private bool _fiveLaneMode;
        [SerializeField]
        private FretArray _fretArray;
        [SerializeField]
        private KickFret _kickFret;

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.29f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, int? currentHighScore)
        {
            // Before we do anything, see if we're in five lane mode or not
            _fiveLaneMode = player.Profile.CurrentInstrument == Instrument.FiveLaneDrums;

            base.Initialize(index, player, chart, trackView, currentHighScore);
        }

        protected override InstrumentDifficulty<DrumNote> GetNotes(SongChart chart)
        {
            var track = chart.GetDrumsTrack(Player.Profile.CurrentInstrument).Clone();
            return track.Difficulties[Player.Profile.CurrentDifficulty];
        }

        protected override DrumsEngine CreateEngine()
        {
            var mode = Player.Profile.CurrentInstrument switch
            {
                Instrument.ProDrums      => DrumsEngineParameters.DrumMode.ProFourLane,
                Instrument.FourLaneDrums => DrumsEngineParameters.DrumMode.NonProFourLane,
                Instrument.FiveLaneDrums => DrumsEngineParameters.DrumMode.FiveLane,
                _                        => throw new Exception("Unreachable.")
            };

            if (!GameManager.IsReplay)
            {
                // Create the engine params from the engine preset
                EngineParams = Player.EnginePreset.Drums.Create(StarMultiplierThresholds, mode);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (DrumsEngineParameters) Player.EngineParameterOverride;
            }

            // The hit window can just be taken from the params
            EngineParams.SetHitWindowScale(GameManager.SelectedSongSpeed);
            HitWindow = EngineParams.HitWindow;

            var engine = new YargDrumsEngine(NoteTrack, SyncTrack, EngineParams);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverhit += OnOverstrum;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnPadHit += (action, wasNoteHit) =>
            {
                // Skip if a note was hit, because we have different logic for that below
                if (wasNoteHit) return;

                // Choose the correct fret
                int fret;
                if (!_fiveLaneMode)
                {
                    fret = action switch
                    {
                        DrumsAction.Kick                                   => 0,
                        DrumsAction.RedDrum                                => 1,
                        DrumsAction.YellowDrum or DrumsAction.YellowCymbal => 2,
                        DrumsAction.BlueDrum or DrumsAction.BlueCymbal     => 3,
                        DrumsAction.GreenDrum or DrumsAction.GreenCymbal   => 4,
                        _                                                  => -1
                    };
                }
                else
                {
                    fret = action switch
                    {
                        DrumsAction.Kick         => 0,
                        DrumsAction.RedDrum      => 1,
                        DrumsAction.YellowCymbal => 2,
                        DrumsAction.BlueDrum     => 3,
                        DrumsAction.OrangeCymbal => 4,
                        DrumsAction.GreenDrum    => 5,
                        _                        => -1
                    };
                }

                // Skip if no animation
                if (fret == -1) return;

                if (fret != 0)
                {
                    _fretArray.PlayDrumAnimation(fret - 1, false);
                }
                else
                {
                    _kickFret.PlayHitAnimation(false);
                }
            };

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            // Get the proper info for four/five lane
            ColorProfile.IFretColorProvider colors = !_fiveLaneMode
                ? Player.ColorProfile.FourLaneDrums
                : Player.ColorProfile.FiveLaneDrums;
            _fretArray.FretCount = !_fiveLaneMode ? 4 : 5;

            _fretArray.Initialize(
                SetupFretTheme(Player.Profile.GameMode),
                colors,
                Player.Profile.LeftyFlip);

            // Particle 0 is always kick fret
            _kickFret.Initialize(
                colors.GetParticleColor(0).ToUnityColor(),
                colors.GetFretColor(0).ToUnityColor(),
                colors.GetFretInnerColor(0).ToUnityColor());
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);
        }

        protected override void ResetVisuals()
        {
            base.ResetVisuals();

            _fretArray.ResetAll();
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, DrumNote note)
        {
            ((DrumsNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, DrumNote note)
        {
            base.OnNoteHit(index, note);

            // Remember that drums treat each note separately

            (NotePool.GetByKey(note) as DrumsNoteElement)?.HitNote();

            // Four and five lane drums have the same kick value
            if (note.Pad != (int) FourLaneDrumPad.Kick)
            {
                int fret;
                if (!_fiveLaneMode)
                {
                    fret = (FourLaneDrumPad) note.Pad switch
                    {
                        FourLaneDrumPad.RedDrum                                    => 0,
                        FourLaneDrumPad.YellowDrum or FourLaneDrumPad.YellowCymbal => 1,
                        FourLaneDrumPad.BlueDrum or FourLaneDrumPad.BlueCymbal     => 2,
                        FourLaneDrumPad.GreenDrum or FourLaneDrumPad.GreenCymbal   => 3,
                        _                                                          => -1
                    };
                }
                else
                {
                    fret = (FiveLaneDrumPad) note.Pad switch
                    {
                        FiveLaneDrumPad.Red    => 0,
                        FiveLaneDrumPad.Yellow => 1,
                        FiveLaneDrumPad.Blue   => 2,
                        FiveLaneDrumPad.Orange => 3,
                        FiveLaneDrumPad.Green  => 4,
                        _                      => -1
                    };
                }

                _fretArray.PlayDrumAnimation(fret, true);
            }
            else
            {
                _kickFret.PlayHitAnimation(true);
                CameraPositioner.Bounce();
            }
        }

        protected override void OnNoteMissed(int index, DrumNote note)
        {
            base.OnNoteMissed(index, note);

            // Remember that drums treat each note separately

            (NotePool.GetByKey(note) as DrumsNoteElement)?.MissNote();
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            return false;
        }
    }
}