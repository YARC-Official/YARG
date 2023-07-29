using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Gameplay.Visuals;
using YARG.Input;
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

        private IEnumerator<Beatline> _beatlineEnumerator;

        protected virtual void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            _replayInputs = new List<GameInput>();

            IsFc = true;
        }

        public virtual void Initialize(YargPlayer player, SongChart chart)
        {
            if (IsInitialized)
                return;

            Player = player;
            SyncTrack = chart.SyncTrack;

            if (GameManager.IsReplay)
            {
                // _replayInputs = new List<GameInput>(GlobalVariables.Instance.CurrentReplay.Frames[0].Inputs);
                // Debug.Log("Initialized replay inputs with " + _replayInputs.Count + " inputs");
            }

            _beatlineEnumerator = SyncTrack.Beatlines.GetEnumerator();
            _beatlineEnumerator.MoveNext();

            IsInitialized = true;
        }

        protected virtual void Update()
        {
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

        protected abstract void UpdateInputs(double inputTime);
        protected abstract void UpdateVisuals(double songTime);
        protected abstract void UpdateNotes(double songTime);

        private void UpdateBeatlines(double songTime)
        {
            while (_beatlineEnumerator.Current?.Time <= songTime + SpawnTimeOffset)
            {
                var beatline = _beatlineEnumerator.Current;

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

                _beatlineEnumerator.MoveNext();
            }
        }

        protected void Start()
        {
            SubscribeToInputEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromInputEvents();
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

        protected InstrumentDifficulty<TNote> Notes { get; private set; }

        protected IEnumerator<TNote> NoteEnumerator { get; private set; }

        private int _replayInputIndex;

        public override void Initialize(YargPlayer player, SongChart chart)
        {
            if (IsInitialized)
                return;

            base.Initialize(player, chart);

            Notes = GetNotes(chart);

            NoteEnumerator = Notes.Notes.GetEnumerator();
            NoteEnumerator.MoveNext();

            Engine = CreateEngine();

            FinishInitialization();
        }

        protected abstract InstrumentDifficulty<TNote> GetNotes(SongChart chart);
        protected abstract TEngine CreateEngine();
        protected abstract void FinishInitialization();

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
            bool groove = stats.ScoreMultiplier is 4 or 8;

            TrackMaterial.SetTrackScroll(songTime, NoteSpeed);
            TrackMaterial.GrooveMode = groove;

            ComboMeter.SetCombo(stats.ScoreMultiplier, stats.IsStarPowerActive ? 8 : 4, stats.Combo);
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

        protected abstract void OnNoteHit(int index, TNote note);
        protected abstract void OnNoteMissed(int index, TNote note);
        protected abstract void OnOverstrum();

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