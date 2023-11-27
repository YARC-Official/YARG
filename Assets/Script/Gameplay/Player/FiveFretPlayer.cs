using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Settings;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        public GuitarEngineParameters EngineParams { get; private set; }

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override BaseStats Stats => Engine?.EngineStats;

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetFiveFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.Difficulties[Player.Profile.CurrentDifficulty];
        }

        protected override GuitarEngine CreateEngine()
        {
            HitWindow = new HitWindowSettings(0.15, 0.04, 1, SettingsManager.Settings.DynamicWindow.Data);
            EngineParams = new GuitarEngineParameters(HitWindow, StarMultiplierThresholds, 0.08, 0.06, 0.025,
                SettingsManager.Settings.InfiniteFrontEnd.Data, SettingsManager.Settings.AntiGhosting.Data);
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
            };

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

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

        protected override void UpdateVisuals(double time)
        {
            base.UpdateVisuals(time);

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
    }
}