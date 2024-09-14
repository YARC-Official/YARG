using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Engine.ProGuitar;
using YARG.Core.Engine.ProGuitar.Engines;
using YARG.Core.Game;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Player;
using YARG.Settings;

namespace YARG.Gameplay.Player
{
    public class ProGuitarPlayer : TrackPlayer<ProGuitarEngine, ProGuitarNote>
    {
        public override bool ShouldUpdateInputsOnResume => true;

        private static float[] GuitarStarMultiplierThresholds => new[]
        {
            0.21f, 0.46f, 0.77f, 1.85f, 3.08f, 4.52f
        };

        private static float[] BassStarMultiplierThresholds => new[]
        {
            0.21f, 0.50f, 0.90f, 2.77f, 4.62f, 6.78f
        };

        [Header("Pro Guitar Specific")]
        [SerializeField]
        private ProStringArray _stringArray;

        public ProGuitarEngineParameters EngineParams { get; private set; }

        public override float[] StarMultiplierThresholds { get; protected set; } =
            GuitarStarMultiplierThresholds;

        public override int[] StarScoreThresholds { get; protected set; }

        public float WhammyFactor { get; private set; }

        protected override bool AllowTheming => false;

        private SongStem _stem;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView,
            StemMixer mixer, int? currentHighScore)
        {
            _stem = player.Profile.CurrentInstrument.ToSongStem();
            if (_stem == SongStem.Bass && mixer[SongStem.Bass] == null)
            {
                _stem = SongStem.Rhythm;
            }

            base.Initialize(index, player, chart, trackView, mixer, currentHighScore);
        }

        protected override InstrumentDifficulty<ProGuitarNote> GetNotes(SongChart chart)
        {
            var track = chart.GetProGuitarTrack(Player.Profile.CurrentInstrument).Clone();
            return track.GetDifficulty(Player.Profile.CurrentDifficulty);
        }

        protected override ProGuitarEngine CreateEngine()
        {
            // If on bass, replace the star multiplier threshold
            bool isBass = Player.Profile.CurrentInstrument is Instrument.ProBass_17Fret or Instrument.ProBass_22Fret;
            if (isBass)
            {
                StarMultiplierThresholds = BassStarMultiplierThresholds;
            }

            if (!GameManager.IsReplay)
            {
                EngineParams = new ProGuitarEngineParameters(
                    new HitWindowSettings(0.14, 0.14, 1.0, false, 0, 0, 0),
                    isBass ? EnginePreset.BASS_MAX_MULTIPLIER : EnginePreset.DEFAULT_MAX_MULTIPLIER,
                    EnginePreset.DEFAULT_WHAMMY_BUFFER,
                    EnginePreset.DEFAULT_SUSTAIN_DROP_LENIENCY,
                    StarMultiplierThresholds, 0.08);
            }
            else
            {
                // Otherwise, get from the replay
                EngineParams = (ProGuitarEngineParameters) Player.EngineParameterOverride;
            }

            var engine = new YargProGuitarEngine(NoteTrack, SyncTrack, EngineParams, Player.Profile.IsBot);

            HitWindow = EngineParams.HitWindow;

            YargLogger.LogFormatDebug("Note count: {0}", NoteTrack.Notes.Count);

            engine.OnNoteHit += OnNoteHit;
            engine.OnNoteMissed += OnNoteMissed;

            engine.OnSustainStart += OnSustainStart;
            engine.OnSustainEnd += OnSustainEnd;

            engine.OnSoloStart += OnSoloStart;
            engine.OnSoloEnd += OnSoloEnd;

            engine.OnStarPowerPhraseHit += OnStarPowerPhraseHit;
            engine.OnStarPowerStatus += OnStarPowerStatus;

            engine.OnCountdownChange += OnCountdownChange;

            return engine;
        }

        protected override void FinishInitialization()
        {
            base.FinishInitialization();

            StarScoreThresholds = PopulateStarScoreThresholds(StarMultiplierThresholds, Engine.BaseScore);
        }

        public override void ResetPracticeSection()
        {
            base.ResetPracticeSection();

            _stringArray.ResetAll();
        }

        protected override void UpdateVisuals(double songTime)
        {
            UpdateBaseVisuals(Engine.EngineStats, EngineParams, songTime);

            _stringArray.UpdatePressed(Engine.HeldFrets);
        }

        public override void SetStemMuteState(bool muted)
        {
            if (IsStemMuted != muted)
            {
                GameManager.ChangeStemMuteState(_stem, muted);
                IsStemMuted = muted;
            }
        }

        public override void SetStarPowerFX(bool active)
        {
            GameManager.ChangeStemReverbState(_stem, active);
        }

        protected override void ResetVisuals()
        {
            base.ResetVisuals();

            _stringArray.ResetAll();
        }

        protected override bool CanSpawnNoteAndChildren(ProGuitarNote note)
        {
            return NotePool.CanSpawnAmount(1);
        }

        protected override void SpawnNoteAndChildren(ProGuitarNote note)
        {
            // For pro guitar, each chord is one object, so we must override this so only the
            // chord parent gets spawned.

            var poolable = NotePool.KeyedTakeWithoutEnabling(note);
            if (poolable == null)
            {
                YargLogger.LogWarning("Attempted to spawn note, but it's at its cap!");
                return;
            }

            InitializeSpawnedNote(poolable, note);
            poolable.EnableFromPool();
        }

        protected override void InitializeSpawnedNote(IPoolable poolable, ProGuitarNote note)
        {
            ((ProGuitarNoteElement) poolable).ChordRef = note;
        }

        protected override void OnNoteHit(int index, ProGuitarNote chordParent)
        {
            base.OnNoteHit(index, chordParent);

            if (GameManager.Paused) return;

            (NotePool.GetByKey(chordParent) as ProGuitarNoteElement)?.HitNote();
        }

        protected override void OnNoteMissed(int index, ProGuitarNote chordParent)
        {
            base.OnNoteMissed(index, chordParent);

            // foreach (var note in chordParent.AllNotes)
            // {
            //     (NotePool.GetByKey(note) as FiveFretNoteElement)?.MissNote();
            // }
        }

        protected override void OnOverhit()
        {
            base.OnOverhit();

            if (SettingsManager.Settings.OverstrumAndOverhitSoundEffects.Value)
            {
                const int MIN = (int) SfxSample.Overstrum1;
                const int MAX = (int) SfxSample.Overstrum4;

                var randomOverstrum = (SfxSample) Random.Range(MIN, MAX + 1);
                GlobalAudioHandler.PlaySoundEffect(randomOverstrum);
            }
        }

        private void OnSustainStart(ProGuitarNote parent)
        {
            foreach (var note in parent.AllNotes)
            {
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                // if (note.Fret != (int) FiveFretGuitarFret.Open)
                // {
                //     _fretArray.SetSustained(note.Fret - 1, true);
                // }
            }
        }

        private void OnSustainEnd(ProGuitarNote parent, double timeEnded, bool finished)
        {
            foreach (var note in parent.AllNotes)
            {
                if (parent.IsDisjoint && parent != note)
                {
                    continue;
                }

                (NotePool.GetByKey(note) as FiveFretNoteElement)?.SustainEnd(finished);

                // if (note.Fret != (int) FiveFretGuitarFret.Open)
                // {
                //     _fretArray.SetSustained(note.Fret - 1, false);
                // }
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
                GameManager.ChangeStemWhammyPitch(_stem, WhammyFactor);
            }
        }
    }
}