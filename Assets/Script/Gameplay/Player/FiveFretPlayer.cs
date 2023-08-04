using UnityEngine;
using YARG.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : BasePlayer<GuitarEngine, GuitarNote>
    {
        private readonly GuitarEngineParameters _engineParams = new(0.16, 1, 0.08, 0.07, 0.035, false, true);

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override float[] StarMultiplierThresholds { get; } =
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        public override int[] StarScoreThresholds { get; protected set; }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetFiveFretTrack(Player.Profile.Instrument);
            return track.Difficulties[Player.Profile.Difficulty];
        }

        protected override GuitarEngine CreateEngine()
        {
            var engine = new YargFiveFretEngine(NoteTrack, SyncTrack, _engineParams);

            Debug.Log("Note count: " + NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverstrum;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnSustainEnd += (parent, timeEnded) =>
            {
                foreach (var note in parent.ChordEnumerator())
                {
                    if(parent.IsDisjoint && parent != note)
                    {
                        continue;
                    }

                    (NotePool.GetByKey(note) as FiveFretNoteElement)?.SustainEnd();
                }
            };

            engine.OnStarPowerPhraseHit += _ =>
            {
                GlobalVariables.AudioManager.PlaySoundEffect(SfxSample.StarPowerAward);
            };

            engine.OnStarPowerStatus += (status) =>
            {
                GlobalVariables.AudioManager.PlaySoundEffect(status ? SfxSample.StarPowerDeploy : SfxSample.StarPowerRelease);
            };

            return engine;
        }

        protected override void FinishInitialization()
        {
            StarScoreThresholds = new int[StarMultiplierThresholds.Length];
            for (int i = 0; i < StarMultiplierThresholds.Length; i++)
            {
                StarScoreThresholds[i] = Mathf.FloorToInt(Engine.BaseScore * StarMultiplierThresholds[i]);
            }

            _fretArray.Initialize(Player.ColorProfile);
            HitWindowDisplay.SetHitWindowInfo(_engineParams, NoteSpeed);
        }

        public override void UpdateWithTimes(double inputTime, double songTime)
        {
            base.UpdateWithTimes(inputTime, songTime);

            Score = Engine.EngineStats.Score;
            Combo = Engine.EngineStats.Combo;
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, songTime);

            for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
            {
                _fretArray.SetPressed((int) fret, Engine.IsFretHeld(fret));
            }
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, GuitarNote note)
        {
            ((FiveFretNoteElement) poolable).NoteRef = note;
        }

        protected override void OnNoteHit(int index, GuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            foreach (var note in chordParent.ChordEnumerator())
            {
                // TODO: It is possible that this should be moved to BasePlayer
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.HitNote();

                if (note.Fret != 0)
                {
                    _fretArray.PlayHitAnimation(note.Fret - 1);
                }
            }
        }

        protected override void OnNoteMissed(int index, GuitarNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);

            foreach (var note in chordParent.ChordEnumerator())
            {
                // TODO: It is possible that this should be moved to BasePlayer
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.MissNote();
            }

            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected override void OnOverstrum()
        {
            base.OnOverstrum();

            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            //if(input.GetAction<GuitarAction>())
            if (input.GetAction<GuitarAction>() == GuitarAction.StarPower && input.Button && GameManager.IsPractice)
            {
                GameManager.PracticeManager.ResetPractice();
                return true;
            }

            return false;
        }
    }
}