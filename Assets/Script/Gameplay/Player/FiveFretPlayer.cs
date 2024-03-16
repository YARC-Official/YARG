using UnityEngine;
using YARG.Audio;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Guitar.Engines;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public sealed class FiveFretPlayer : TrackPlayer<GuitarEngine, GuitarNote>
    {
        private const double SUSTAIN_END_MUTE_THRESHOLD = 0.1;

        public override bool ShouldUpdateInputsOnResume => true;

        private static float[] GuitarStarMultiplierThresholds => new[]
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        private static float[] BassStarMultiplierThresholds => new[]
        {
            0.21f, 0.50f, 0.90f, 2.77f, 4.62f, 6.78f
        };

        public GuitarEngineParameters EngineParams { get; private set; }

        [Header("Five Fret Specific")]
        [SerializeField]
        private FretArray _fretArray;

        public override float[] StarMultiplierThresholds { get; protected set; } =
            GuitarStarMultiplierThresholds;

        public override int[] StarScoreThresholds { get; protected set; }

        public float WhammyFactor { get; private set; }

        private SongStem _stem;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView, StemMixer mixer, int? currentHighScore)
        {
            _stem = player.Profile.CurrentInstrument.ToSongStem();
            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);
        }

        protected override InstrumentDifficulty<GuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetFiveFretTrack(Player.Profile.CurrentInstrument).Clone();
            return track.Difficulties[Player.Profile.CurrentDifficulty];
        }

        protected override GuitarEngine CreateEngine()
        {
            // If on bass, replace the star multiplier threshold
            bool isBass = Player.Profile.CurrentInstrument == Instrument.FiveFretBass;
            if (isBass)
            {
                StarMultiplierThresholds = BassStarMultiplierThresholds;
            }

            if (!GameManager.IsReplay)
            {
                // Create the engine params from the engine preset
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

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;
            engine.OnOverstrum += OnOverstrum;

            engine.OnSustainStart += OnSustainStart;
            engine.OnSustainEnd += OnSustainEnd;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);

            IndicatorStripes.Initialize(Player.EnginePreset.FiveFretGuitar);
            _fretArray.Initialize(
                Player.ThemePreset,
                Player.Profile.GameMode,
                Player.ColorProfile.FiveFretGuitar,
                Player.Profile.LeftyFlip);
        }

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();

            _fretArray.ResetAll();
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);

            for (var fret = GuitarAction.GreenFret; fret <= GuitarAction.OrangeFret; fret++)
            {
                _fretArray.SetPressed((int) fret, Engine.IsFretHeld(fret));
            }
        }

        public override void SetStemMuteState(bool muted)
        {
            if (_isStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(_stem, muted);
                _isStemMuted = muted;
            }
        }

        public override void SetStarPowerFX(bool active)
        {
            GameManager.ChangeStemReverbState(_stem, active);
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

        protected override void OnOverstrum()
        {
            base.OnOverstrum();

            const int min = (int) SfxSample.Overstrum1;
            const int max = (int) SfxSample.Overstrum4;

            var randomOverstrum = (SfxSample) Random.Range(min, max + 1);

            AudioManager.PlaySoundEffect(randomOverstrum);
        }

        private void OnSustainStart(GuitarNote parent)
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
        }

        private void OnSustainEnd(GuitarNote parent, double timeEnded, bool finished)
        {
            foreach (var note in parent.ChordEnumerator())
            {
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                (NotePool.GetByKey(note) as FiveFretNoteElement)?.SustainEnd(finished);

                if (note.Fret != 0)
                {
                    _fretArray.SetSustained(note.Fret - 1, false);
                }
            }

            // Mute the stem if you let go of the sustain too early.
            // Leniency is handled by the engine's sustain burst threshold.
            if (!parent.IsDisjoint && !finished)
            {
                SetStemMuteState(true);
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