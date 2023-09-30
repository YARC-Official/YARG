using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Gameplay.HUD;
using YARG.Gameplay.Visuals;
using YARG.Player;

namespace YARG.Gameplay.Player
{
    public abstract class TrackPlayer : BasePlayer
    {
        public const float STRIKE_LINE_POS       = -2f;
        public const float DEFAULT_ZERO_FADE_POS = 3f;
        public const float NOTE_SPAWN_OFFSET     = 5f;

        public const float TRACK_WIDTH = 2f;

        public double SpawnTimeOffset => (ZeroFadePosition + 2 + -STRIKE_LINE_POS) / NoteSpeed;

        protected TrackView TrackView { get; private set; }

        [field: Header("Visuals")]
        [field: SerializeField]
        public Camera TrackCamera { get; private set; }

        [SerializeField]
        protected CameraPositioner CameraPositioner;
        [SerializeField]
        protected TrackMaterial TrackMaterial;
        [SerializeField]
        protected ComboMeter ComboMeter;
        [SerializeField]
        protected StarpowerBar StarpowerBar;
        [SerializeField]
        protected SunburstEffects SunburstEffects;
        [SerializeField]
        protected HitWindowDisplay HitWindowDisplay;

        [Header("Pools")]
        [SerializeField]
        protected KeyedPool NotePool;
        [SerializeField]
        protected Pool BeatlinePool;

        public float ZeroFadePosition { get; private set; }
        public float FadeSize         { get; private set; }

        protected List<Beatline> Beatlines;
        protected int BeatlineIndex;

        public virtual void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView)
        {
            if (IsInitialized) return;

            Initialize(index, player, chart);

            TrackView = trackView;

            Beatlines = SyncTrack.Beatlines;
            BeatlineIndex = 0;

            ZeroFadePosition = DEFAULT_ZERO_FADE_POS * Player.Profile.HighwayLength;
            FadeSize = Player.CameraPreset.FadeLength;

            if (player.Profile.HighwayLength > 1)
            {
                FadeSize *= player.Profile.HighwayLength;
            }
        }

        protected override void ResetVisuals()
        {
            ComboMeter.SetFullCombo(IsFc);
            TrackView.ForceReset();

            NotePool.ReturnAllObjects();
            BeatlinePool.ReturnAllObjects();
        }

        protected override void UpdateVisualsWithTimes(double time)
        {
            base.UpdateVisualsWithTimes(time);
            UpdateNotes(time);
            UpdateBeatlines(time);
        }

        protected abstract void UpdateNotes(double time);

        private void UpdateBeatlines(double time)
        {
            while (BeatlineIndex < Beatlines.Count && Beatlines[BeatlineIndex].Time <= time + SpawnTimeOffset)
            {
                var beatline = Beatlines[BeatlineIndex];

                // Skip this frame if the pool is full
                if (!BeatlinePool.CanSpawnAmount(1))
                {
                    break;
                }

                var poolable = BeatlinePool.TakeWithoutEnabling();
                if (poolable == null)
                {
                    Debug.LogWarning("Attempted to spawn beatline, but it's at its cap!");
                    break;
                }

                ((BeatlineElement) poolable).BeatlineRef = beatline;
                poolable.EnableFromPool();

                BeatlineIndex++;
            }
        }
    }

    public abstract class TrackPlayer<TEngine, TNote> : TrackPlayer
        where TEngine : BaseEngine
        where TNote : Note<TNote>
    {
        private int _replayInputIndex;

        public TEngine Engine { get; private set; }

        protected List<TNote> Notes { get; private set; }

        protected int NoteIndex { get; private set; }

        protected InstrumentDifficulty<TNote> NoteTrack { get; private set; }

        private InstrumentDifficulty<TNote> OriginalNoteTrack { get; set; }

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart, trackView);

            OriginalNoteTrack = GetNotes(chart);
            player.Profile.ApplyModifiers(OriginalNoteTrack);

            NoteTrack = OriginalNoteTrack;
            Notes = NoteTrack.Notes;

            Engine = CreateEngine();

            ResetNoteCounters();

            FinishInitialization();
        }

        protected abstract InstrumentDifficulty<TNote> GetNotes(SongChart chart);
        protected abstract TEngine CreateEngine();

        protected virtual void FinishInitialization()
        {
            GameManager.BeatEventManager.Subscribe(StarpowerBar.PulseBarIfAble,
                new BeatEventManager.Info(1f / 4f, 0f));

            TrackMaterial.Initialize(ZeroFadePosition, FadeSize);
            CameraPositioner.Initialize(Player.CameraPreset);
        }

        protected void ResetNoteCounters()
        {
            NoteIndex = 0;
            NotesHit = 0;
            TotalNotes = Notes.Sum(i => Engine.GetNumberOfNotes(i));
        }

        public override void ResetPracticeSection()
        {
            Engine.Reset(true);

            if (NoteTrack.Notes.Count > 0)
            {
                NoteTrack.Notes[0].OverridePreviousNote();
                NoteTrack.Notes[^1].OverrideNextNote();
            }

            IsFc = true;

            BeatlineIndex = 0;
            ResetNoteCounters();

            ResetVisuals();
        }

        protected override void UpdateInputs(double inputTime)
        {
            if (Player.Profile.IsBot)
            {
                Engine.UpdateBot(inputTime);
                return;
            }

            if (GameManager.IsReplay)
            {
                while (_replayInputIndex < ReplayInputs.Count && inputTime >= ReplayInputs[_replayInputIndex].Time)
                {
                    Engine.QueueInput(ReplayInputs[_replayInputIndex++]);
                }
            }

            if (Engine.IsInputQueued)
            {
                Engine.UpdateEngine();
            }
            else
            {
                Engine.UpdateEngine(inputTime);
            }
        }

        protected void UpdateBaseVisuals(BaseStats stats, double songTime)
        {
            int maxMultiplier = stats.IsStarPowerActive ? 8 : 4;
            bool groove = stats.ScoreMultiplier == maxMultiplier;

            TrackMaterial.SetTrackScroll(songTime, NoteSpeed);
            TrackMaterial.GrooveMode = groove;
            TrackMaterial.StarpowerMode = stats.IsStarPowerActive;

            ComboMeter.SetCombo(stats.ScoreMultiplier, maxMultiplier, stats.Combo);
            StarpowerBar.SetStarpower(stats.StarPowerAmount, stats.IsStarPowerActive);
            SunburstEffects.SetSunburstEffects(groove, stats.IsStarPowerActive);

            TrackView.UpdateNoteStreak(stats.Combo);
        }

        protected override void UpdateNotes(double songTime)
        {
            while (NoteIndex < Notes.Count && Notes[NoteIndex].Time <= songTime + SpawnTimeOffset)
            {
                var note = Notes[NoteIndex];

                // Skip this frame if the pool is full
                if (!NotePool.CanSpawnAmount(note.ChildNotes.Count + 1))
                {
                    break;
                }

                NoteIndex++;

                // Don't spawn hit or missed notes
                if (note.WasHit || note.WasMissed)
                {
                    continue;
                }

                // Spawn all of the notes and child notes
                foreach (var child in note.ChordEnumerator())
                {
                    SpawnNote(child);
                }
            }
        }

        public override void SetPracticeSection(uint start, uint end)
        {
            var practiceNotes = OriginalNoteTrack.Notes.Where(n => n.Tick >= start && n.Tick < end).ToList();

            Debug.Log($"Practice notes: {practiceNotes.Count}");

            var instrument = OriginalNoteTrack.Instrument;
            var difficulty = OriginalNoteTrack.Difficulty;
            var phrases = OriginalNoteTrack.Phrases;
            var textEvents = OriginalNoteTrack.TextEvents;

            NoteTrack = new InstrumentDifficulty<TNote>(instrument, difficulty, practiceNotes, phrases, textEvents);
            Notes = NoteTrack.Notes;

            ResetNoteCounters();

            Beatlines = SyncTrack.Beatlines.Where(b => b.Tick >= start && b.Tick <= end).ToList();
            BeatlineIndex = 0;

            Engine = CreateEngine();
            ResetPracticeSection();
        }

        public override void SetReplayTime(double time)
        {
            ResetVisuals();

            IsFc = true;

            BeatlineIndex = 0;
            ResetNoteCounters();

            _replayInputIndex = Engine.ProcessUpToTime(time, ReplayInputs);
            UpdateVisualsWithTimes(time);
        }

        protected void SpawnNote(TNote note)
        {
            var poolable = NotePool.KeyedTakeWithoutEnabling(note);
            if (poolable == null)
            {
                Debug.LogWarning("Attempted to spawn note, but it's at its cap!");
                return;
            }

            InitializeSpawnedNote(poolable, note);
            poolable.EnableFromPool();
        }

        protected abstract void InitializeSpawnedNote(IPoolable poolable, TNote note);

        protected virtual void OnNoteHit(int index, TNote note)
        {
            NotesHit++;
        }

        protected virtual void OnNoteMissed(int index, TNote note)
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected virtual void OnOverstrum()
        {
            if (IsFc)
            {
                ComboMeter.SetFullCombo(false);
                IsFc = false;
            }
        }

        protected virtual void OnSoloStart(SoloSection solo)
        {
            TrackView.StartSolo(solo);
        }

        protected virtual void OnSoloEnd(SoloSection solo)
        {
            TrackView.EndSolo(0);
        }

        protected override void FinishDestruction()
        {
            base.FinishDestruction();

            GameManager.BeatEventManager.Unsubscribe(StarpowerBar.PulseBarIfAble);
        }

        protected override void OnInputProcessed(ref GameInput input)
        {
            Engine.QueueInput(input);
        }
    }
}