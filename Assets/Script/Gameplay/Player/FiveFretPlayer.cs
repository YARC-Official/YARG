using UnityEngine;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        private const double SUSTAIN_END_MUTE_THRESHOLD = 0.1;

        public GuitarEngineParameters EngineParams { get; private set; }

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        public float WhammyFactor { get; private set; }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetFiveFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.Difficulties[Player.Profile.CurrentDifficulty];
        }

        protected override GuitarEngine CreateEngine()
        {
            if (!GameManager.IsReplay)
            {
                // Create the engine params from the engine preset
                bool isBass = Player.Profile.CurrentInstrument == Instrument.FiveFretBass;
                EngineParams = Player.EnginePreset.FiveFretGuitar.Create(StarMultiplierThresholds, isBass);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (GuitarEngineParameters) Player.EngineParameterOverride;
            }

            // The hit window can just be taken from the params
            EngineParams.SetHitWindowScale(GameManager.SelectedSongSpeed);
            HitWindow = EngineParams.HitWindow;

            var engine = new YargFiveFretEngine(NoteTrack, SyncTrack, EngineParams);

            Debug.Log("Note count: " + NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverstrum;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnSustainStart += (parent) =>
            {
                foreach (var note in parent.ChordEnumerator())
                {
                    if (parent.IsDisjoint && parent != note)
                    {
                        continue;
                    }

                    if (note.Fret != 0)
                    {
                        _fretArray.SetSustained(note.Fret - 1, true);
                    }
                }
            };

            engine.OnSustainEnd += (parent, timeEnded) =>
            {
                foreach (var note in parent.ChordEnumerator())
                {
                    if (parent.IsDisjoint && parent != note)
                    {
                        continue;
                    }

                    (NotePool.GetByKey(note) as FiveFretNoteElement)?.SustainEnd();

                    if (note.Fret != 0)
                    {
                        _fretArray.SetSustained(note.Fret - 1, false);
                    }
                }

                // Mute the stem if you let go of the sustain too early.
                // Add a small threshold to prevent the stem from muting
                // if you let go a little bit too early.
                if (!parent.IsDisjoint && parent.TimeEnd - timeEnded > SUSTAIN_END_MUTE_THRESHOLD)
                {
                    ShouldMuteStem = true;
                }
            };

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            IndicatorStripes.Initialize(Player.EnginePreset.FiveFretGuitar);
            _fretArray.Initialize(
                SetupFretTheme(Player.Profile.GameMode),
                Player.ColorProfile.FiveFretGuitar,
                Player.Profile.LeftyFlip);
        }

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();

            for (int i = 0; i < _fretArray.Frets.Count; i++)
            {
                _fretArray.SetSustained(i, false);
            }
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);

            for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
            {
                _fretArray.SetPressed((int) fret, Engine.IsFretHeld(fret));
            }
        }

        protected override void ResetVisuals()
        {
            base.ResetVisuals();

            _fretArray.ResetAll();
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            ((FiveFretNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            if (GameManager.Paused) return;

            foreach (var note in chordParent.ChordEnumerator())
            {
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.HitNote();

                if (note.Fret != 0)
                {
                    _fretArray.PlayHitAnimation(note.Fret - 1);
                }
                else
                {
                    _fretArray.PlayOpenHitAnimation();
                }
            }
        }

        protected override void OnNoteMissed(int index, GuitarNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);

            foreach (var note in chordParent.ChordEnumerator())
            {
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.MissNote();
            }
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            // Ignore SP in practice mode
            if (input.GetAction<GuitarAction>() == GuitarAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }

        protected override void OnInputQueued(GameInput input)
        {
            base.OnInputQueued(input);

            // Update the whammy factor
            if (input.GetAction<GuitarAction>() == GuitarAction.Whammy)
            {
                WhammyFactor = Mathf.Clamp01(input.Axis);
            }
        }
    }
}