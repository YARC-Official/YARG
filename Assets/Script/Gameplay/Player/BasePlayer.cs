using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Input;
using YARG.Player;

namespace YARG.Gameplay
{
    public abstract class BasePlayer : MonoBehaviour
    {
        [field: Header("Visuals")]
        [field: SerializeField]
        public Camera TrackCamera { get; private set; }

        [SerializeField]
        protected ComboMeter ComboMeter;
        [SerializeField]
        protected StarpowerBar StarpowerBar;

        protected GameManager GameManager { get; private set; }

        private List<GameInput> _replayInputs;

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        public YargPlayer Player;

        protected bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            _replayInputs = new List<GameInput>();
        }

        protected void Initialize(YargPlayer player)
        {
            Player = player;

            IsInitialized = true;
        }

        protected void Update()
        {
            if (GameManager.Paused)
            {
                return;
            }

            UpdateInputs();
            UpdateVisuals();
        }

        protected abstract void UpdateInputs();

        protected abstract void UpdateVisuals();

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

        protected List<TNote> Notes { get; private set; }

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

        public virtual void Initialize(YargPlayer player, List<TNote> notes)
        {
            if (IsInitialized)
            {
                return;
            }

            Initialize(player);

            Notes = notes;
        }
    }
}