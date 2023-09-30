using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.Player
{
    public class DrumsPlayer : TrackPlayer<DrumsEngine, DrumNote>
    {
        public DrumsEngineParameters EngineParams { get; private set; }

        [Header("Drums Specific")]
        [SerializeField]
        private FretArray _fretArray;
        [SerializeField]
        private KickFret _kickFret;

        public override BaseStats Stats => Engine?.EngineStats;

        public override float[] StarMultiplierThresholds { get; }  =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.29f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        protected override InstrumentDifficulty<DrumNote> GetNotes(SongChart chart)
        {
            var track = chart.GetDrumsTrack(Player.Profile.CurrentInstrument).Clone();
            return track.Difficulties[Player.Profile.CurrentDifficulty];
        }

        protected override DrumsEngine CreateEngine()
        {
            EngineParams = new DrumsEngineParameters(0.15, 1, StarMultiplierThresholds);
            var engine = new YargDrumsEngine(NoteTrack, SyncTrack, EngineParams);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverhit += OnOverstrum;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnPadHit += (action, wasNoteHit) =>
            {
                // Skip if a note was hit, because we have different logic for that below
                if (wasNoteHit) return;

                int fret = action switch
                {
                    DrumsAction.Kick                         => 0,
                    DrumsAction.Drum1                        => 1,
                    DrumsAction.Drum2 or DrumsAction.Cymbal1 => 2,
                    DrumsAction.Drum3 or DrumsAction.Cymbal2 => 3,
                    DrumsAction.Drum4 or DrumsAction.Cymbal3 => 4,
                    _                                        => -1
                };

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

            StarScoreThresholds = new int[StarMultiplierThresholds.Length];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarScoreThresholds[i] = Mathf.FloorToInt(Engine.BaseScore * StarMultiplierThresholds[i]);
            }

            _fretArray.Initialize(Player.ColorProfile.FourLaneDrums, Player.Profile.LeftyFlip);
            _kickFret.Initialize(Player.ColorProfile.FourLaneDrums.KickParticles.ToUnityColor());
            HitWindowDisplay.SetHitWindowInfo(EngineParams, NoteSpeed);
        }

        public override void UpdateWithTimes(double inputTime)
        {
            base.UpdateWithTimes(inputTime);

            Score = Engine.EngineStats.Score;
            Combo = Engine.EngineStats.Combo;
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, songTime);
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

            if (note.Pad != 0)
            {
                int fret = (FourLaneDrumPad) note.Pad switch
                {
                    FourLaneDrumPad.RedDrum                                    => 0,
                    FourLaneDrumPad.YellowDrum or FourLaneDrumPad.YellowCymbal => 1,
                    FourLaneDrumPad.BlueDrum or FourLaneDrumPad.BlueCymbal     => 2,
                    FourLaneDrumPad.GreenDrum or FourLaneDrumPad.GreenCymbal   => 3,
                    _                                                          => -1
                };

                _fretArray.PlayDrumAnimation(fret, true);
            }
            else
            {
                _kickFret.PlayHitAnimation(true);
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