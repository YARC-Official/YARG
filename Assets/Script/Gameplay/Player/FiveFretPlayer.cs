using UnityEngine;
using YARG.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Settings;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : BasePlayer<GuitarEngine, GuitarNote>
    {
        private GuitarEngineParameters _engineParams;

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
            var track = chart.GetFiveFretTrack(Player.Profile.Instrument).Clone();
            return track.Difficulties[Player.Profile.Difficulty];
        }

        protected override GuitarEngine CreateEngine()
        {
            _engineParams = new GuitarEngineParameters(0.15, 1, 0.08, 0.06, 0.025,
                SettingsManager.Settings.InfiniteFrontEnd.Data, SettingsManager.Settings.AntiGhosting.Data);
            var engine = new YargFiveFretEngine(NoteTrack, SyncTrack, _engineParams);

            Debug.Log("Note count: " + NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverstrum;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

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

            engine.OnStarPowerPhraseHit += _ =>
            {
                GlobalVariables.AudioManager.PlaySoundEffect(SfxSample.StarPowerAward);
            };

            engine.OnStarPowerStatus += (status) =>
            {
                GlobalVariables.AudioManager.PlaySoundEffect(status
                    ? SfxSample.StarPowerDeploy
                    : SfxSample.StarPowerRelease);
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

            _fretArray.Initialize(Player.ColorProfile, Player.Profile.LeftyFlip);
            HitWindowDisplay.SetHitWindowInfo(_engineParams, NoteSpeed);
        }

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();

            for(int i = 0; i < _fretArray.Frets.Count; i++)
            {
                _fretArray.SetSustained(i, false);
            }
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
                // TODO: It is possible that this should be moved to BasePlayer
                (NotePool.GetByKey(note) as FiveFretNoteElement)?.MissNote();
            }
        }

        protected override void OnOverstrum()
        {
            base.OnOverstrum();
        }

        protected override bool InterceptInput(ref GameInput input)
        {
            // Ignore SP in practice mode
            if (input.GetAction<GuitarAction>() == GuitarAction.StarPower && GameManager.IsPractice) return true;

            return false;
        }
    }
}