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
        public const float SPAWN_OFFSET = 5f;

        public const float TRACK_WIDTH = 2f;

        public double SpawnTimeOffset => (SPAWN_OFFSET + -STRIKE_LINE_POS) / Player.Profile.NoteSpeed;

        [field: Header("Visuals")]
        [field: SerializeField]
        public Camera TrackCamera { get; private set; }

        [SerializeField]
        protected ComboMeter ComboMeter;
        [SerializeField]
        protected StarpowerBar StarpowerBar;

        [Header("Pools")]
        [SerializeField]
        protected Pool NotePool;

        protected GameManager GameManager { get; private set; }

        private List<GameInput> _replayInputs;

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        public YargPlayer Player;

        protected bool IsFc;

        protected bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            _replayInputs = new List<GameInput>();

            IsFc = true;
        }

        protected void Initialize(YargPlayer player)
        {
            Player = player;

            IsInitialized = true;
        }

        protected virtual void Update()
        {
            if (GameManager.Paused)
            {
                return;
            }

            UpdateInputs();
            UpdateVisuals();
            UpdateNotes();
        }

        protected abstract void UpdateInputs();
        protected abstract void UpdateVisuals();
        protected abstract void UpdateNotes();

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
        public TEngine Engine { get; protected set; }

        protected InstrumentDifficulty<TNote> Chart { get; private set; }
        protected IEnumerator<TNote> NoteEnumerator { get; private set; }

        public virtual void Initialize(YargPlayer player, InstrumentDifficulty<TNote> chart)
        {
            if (IsInitialized)
            {
                return;
            }

            Initialize(player);

            Chart = chart;

            NoteEnumerator = Chart.Notes.GetEnumerator();
            NoteEnumerator.MoveNext();
        }

        protected override void UpdateInputs()
        {
            if (Engine.IsInputQueued)
            {
                Engine.UpdateEngine();
            }
            else
            {
                Engine.UpdateEngine(InputManager.BeforeUpdateTime);
            }
        }

        protected void UpdateBaseVisuals(BaseStats stats)
        {
            ComboMeter.SetCombo(stats.ScoreMultiplier, 4, stats.Combo);
            StarpowerBar.SetStarpower(stats.StarPowerAmount);
        }

        protected override void UpdateNotes()
        {
            while (NoteEnumerator.Current?.Time <= GameManager.SongTime + SpawnTimeOffset)
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
            var poolable = NotePool.TakeWithoutEnabling();
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
    }
}