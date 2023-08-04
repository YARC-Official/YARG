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

        public float NoteSpeed => Player.Profile.NoteSpeed / GameManager.SelectedSongSpeed;

        public abstract float[] StarMultiplierThresholds { get; }

        public abstract int[] StarScoreThresholds { get; protected set; }

        public int Score { get; protected set; }
        public int Combo { get; protected set; }

        protected bool IsFc;

        protected bool IsInitialized { get; private set; }

        protected IEnumerator<Beatline> BeatlineEnumerator;

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

            BeatlineEnumerator = SyncTrack.Beatlines.GetEnumerator();
            BeatlineEnumerator.MoveNext();

            IsInitialized = true;
        }

        public virtual void UpdateWithTimes(double inputTime, double songTime)
        {
            if (GameManager.Paused)
            {
                return;
            }

            UpdateInputs(inputTime);
            UpdateVisuals(songTime);
            UpdateNotes(songTime);
            UpdateBeatlines(songTime);
        }

        public abstract void SetPracticeSection(double timeStart, double timeEnd);

        public abstract void ResetPracticeSection();

        protected abstract void UpdateInputs(double inputTime);
        protected abstract void UpdateVisuals(double songTime);
        protected abstract void UpdateNotes(double songTime);

        private void UpdateBeatlines(double songTime)
        {
            while (BeatlineEnumerator.Current?.Time <= songTime + SpawnTimeOffset)
            {
                var beatline = BeatlineEnumerator.Current;

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

                BeatlineEnumerator.MoveNext();
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
        public TEngine Engine { get; private set; }

        private   InstrumentDifficulty<TNote> OriginalNoteTrack { get; set; }
        protected InstrumentDifficulty<TNote> NoteTrack     { get; private set; }

        protected IEnumerator<TNote> NoteEnumerator { get; private set; }

        private int _replayInputIndex;

        public override void Initialize(int index, YargPlayer player, SongChart chart, TrackView trackView)
        {
            if (IsInitialized) return;

            base.Initialize(index, player, chart, trackView);

            OriginalNoteTrack = GetNotes(chart);
            NoteTrack = OriginalNoteTrack;

            NoteEnumerator = NoteTrack.Notes.GetEnumerator();
            NoteEnumerator.MoveNext();

            Engine = CreateEngine();

            FinishInitialization();
        }

        protected abstract InstrumentDifficulty<TNote> GetNotes(SongChart chart);
        protected abstract TEngine CreateEngine();
        protected abstract void FinishInitialization();

        public override void SetPracticeSection(double timeStart, double timeEnd)
        {
            var practiceNotes = OriginalNoteTrack.Notes.Where(n => n.Time >= timeStart && n.Time < timeEnd).ToList();

            Debug.Log($"Practice notes: {practiceNotes.Count}");

            var instrument = OriginalNoteTrack.Instrument;
            var difficulty = OriginalNoteTrack.Difficulty;
            var phrases = OriginalNoteTrack.Phrases;
            var textEvents = OriginalNoteTrack.TextEvents;

            NoteTrack = new InstrumentDifficulty<TNote>(instrument, difficulty, practiceNotes, phrases, textEvents);

            NoteEnumerator = NoteTrack.Notes.GetEnumerator();
            NoteEnumerator.MoveNext();

            BeatlineEnumerator = SyncTrack.Beatlines.Where(b => b.Time >= timeStart && b.Time <= timeEnd).GetEnumerator();
            BeatlineEnumerator.MoveNext();

            Engine = CreateEngine();
            ResetPracticeSection();

            if (practiceNotes.Count > 0)
            {
                practiceNotes[0].OverridePreviousNote();
                practiceNotes[^1].OverrideNextNote();
            }
        }

        public override void ResetPracticeSection()
        {
            Engine.Reset();

            IsFc = true;
            ComboMeter.SetFullCombo(true);
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
        }

        protected override void UpdateNotes(double songTime)
        {
            while (NoteEnumerator.Current?.Time <= songTime + SpawnTimeOffset)
            {
                var note = NoteEnumerator.Current;

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

                NoteEnumerator.MoveNext();
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
        }

        protected virtual void OnNoteMissed(int index, TNote note)
        {
        }

        protected virtual void OnOverstrum()
        {
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

            double adjustedTime = input.Time * GameManager.SelectedSongSpeed;
            input = new(adjustedTime, input.Action, input.Integer);
            Engine.QueueInput(input);
            AddReplayInput(input);
        }
    }
}