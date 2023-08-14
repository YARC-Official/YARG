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
    public abstract class BasePlayer : MonoBehaviour
    {
        public const float STRIKE_LINE_POS = -2f;
        public const float SPAWN_OFFSET    = 5f;

        public const float TRACK_WIDTH = 2f;

        public double SpawnTimeOffset => (SPAWN_OFFSET + -STRIKE_LINE_POS) / NoteSpeed;

        [field: Header("Visuals")]
        [field: SerializeField]
        public Camera TrackCamera { get; private set; }

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

        protected GameManager GameManager { get; private set; }
        protected TrackView   TrackView   { get; private set; }

        protected SyncTrack SyncTrack { get; private set; }

        private List<GameInput> _replayInputs;

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        public YargPlayer Player { get; private set; }

        public float NoteSpeed
        {
            get
            {
                if (GameManager.IsPractice && GameManager.SelectedSongSpeed < 1)
                {
                    return Player.Profile.NoteSpeed;
                }

                return Player.Profile.NoteSpeed / GameManager.SelectedSongSpeed;
            }
        }

        public abstract float[] StarMultiplierThresholds { get; }

        public abstract int[] StarScoreThresholds { get; protected set; }

        public int Score { get; protected set; }
        public int Combo { get; protected set; }

        public int NotesHit   { get; protected set; }
        public int TotalNotes { get; protected set; }

        protected bool IsFc;

        protected bool IsInitialized { get; private set; }

        protected List<Beatline> Beatlines;

        protected int BeatlineIndex;

        protected virtual void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            _replayInputs = new List<GameInput>();

            IsFc = true;
        }

        public virtual void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView)
        {
            if (IsInitialized) return;

            Player = player;
            TrackView = trackView;

            SyncTrack = chart.SyncTrack;

            if (GameManager.IsReplay)
            {
                _replayInputs = new List<GameInput>(GameManager.Replay.Frames[index].Inputs);
                Debug.Log("Initialized replay inputs with " + _replayInputs.Count + " inputs");
            }

            Beatlines = SyncTrack.Beatlines;
            BeatlineIndex = 0;

            IsInitialized = true;
        }

        public virtual void UpdateWithTimes(double inputTime, double songTime)
        {
            if (GameManager.Paused)
            {
                return;
            }

            UpdateInputs(inputTime);
            UpdateVisuals(inputTime);
            UpdateNotes(inputTime);
            UpdateBeatlines(inputTime);
        }

        public abstract void SetPracticeSection(uint start, uint end);

        public abstract void ResetPracticeSection();

        protected abstract void UpdateInputs(double inputTime);
        protected abstract void UpdateVisuals(double songTime);
        protected abstract void UpdateNotes(double songTime);

        private void UpdateBeatlines(double songTime)
        {
            while (BeatlineIndex < Beatlines.Count && Beatlines[BeatlineIndex].Time <= songTime + SpawnTimeOffset)
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

        protected void Start()
        {
            if (!GameManager.IsReplay)
            {
                SubscribeToInputEvents();
            }
        }

        private void OnDestroy()
        {
            if (!GameManager.IsReplay)
            {
                UnsubscribeFromInputEvents();
            }
        }

        protected void AddReplayInput(GameInput input)
        {
            _replayInputs.Add(input);
        }

        protected abstract void SubscribeToInputEvents();
        protected abstract void UnsubscribeFromInputEvents();
    }

    public abstract class BasePlayer<TEngine, TNote> : BasePlayer
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
            NoteTrack = OriginalNoteTrack;

            Notes = NoteTrack.Notes;
            NoteIndex = 0;
            TotalNotes = Notes.Count;

            Engine = CreateEngine();

            FinishInitialization();
        }

        protected abstract InstrumentDifficulty<TNote> GetNotes(SongChart chart);
        protected abstract TEngine CreateEngine();
        protected abstract void FinishInitialization();

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
            NoteIndex = 0;
            NotesHit = 0;
            TotalNotes = Notes.Count;

            Beatlines = SyncTrack.Beatlines.Where(b => b.Tick >= start && b.Tick <= end).ToList();
            BeatlineIndex = 0;

            Engine = CreateEngine();
            ResetPracticeSection();
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
            ComboMeter.SetFullCombo(true);

            TrackView.ForceEndSolo();

            NoteIndex = 0;
            BeatlineIndex = 0;
            NotesHit = 0;
            TotalNotes = Notes.Count;

            NotePool.ReturnAllObjects();
            BeatlinePool.ReturnAllObjects();
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
            StarpowerBar.SetStarpower(stats.StarPowerAmount);
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

                SpawnNote(note);
                foreach (var child in note.ChildNotes)
                {
                    SpawnNote(child);
                }

                NoteIndex++;
            }
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

        protected override void SubscribeToInputEvents()
        {
            Player.Bindings.SubscribeToGameplayInputs(Player.Profile.GameMode, OnGameInput);
        }

        protected override void UnsubscribeFromInputEvents()
        {
            Player.Bindings.UnsubscribeFromGameplayInputs(Player.Profile.GameMode, OnGameInput);
        }

        protected void OnGameInput(ref GameInput input)
        {
            if (GameManager.Paused)
            {
                return;
            }

            if (InterceptInput(ref input))
            {
                return;
            }

            double adjustedTime = GameManager.GetRelativeInputTime(input.Time);
            input = new(adjustedTime, input.Action, input.Integer);
            Engine.QueueInput(input);
            AddReplayInput(input);
        }

        protected abstract bool InterceptInput(ref GameInput input);
    }
}