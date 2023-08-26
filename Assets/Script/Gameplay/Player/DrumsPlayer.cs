using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Drums.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public class DrumsPlayer : BasePlayer<DrumsEngine, DrumNote>
    {
        private DrumsEngineParameters _engineParams;

        [Header("Drums Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override float[] StarMultiplierThresholds { get; }  =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.29f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        protected override InstrumentDifficulty<DrumNote> GetNotes(SongChart chart)
        {
            var track = chart.GetDrumsTrack(Player.Profile.Instrument).Clone();
            return track.Difficulties[Player.Profile.Difficulty];
        }

        protected override DrumsEngine CreateEngine()
        {
            _engineParams = new DrumsEngineParameters(0.15, 1);
            var engine = new YargDrumsEngine(NoteTrack, SyncTrack, _engineParams);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverhit += OnOverstrum;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

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
            HitWindowDisplay.SetHitWindowInfo(_engineParams, NoteSpeed);
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, songTime);
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

                _fretArray.PlayHitAnimation(fret);
            }
            else
            {
                _fretArray.PlayOpenHitAnimation();
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

        protected override void OnInputProcessed(ref GameInput input)
        {
            base.OnInputProcessed(ref input);

            if (!input.Button) return;

            var action = input.GetAction<DrumsAction>();
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

            if (fret == 0)
            {
                _fretArray.PlayOpenHitAnimation();
            }
            else
            {
                _fretArray.PlayDrumAnimation(fret - 1);
            }
        }
    }
}