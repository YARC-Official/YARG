using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Core.Input;
using YARG.Player;

namespace YARG.Gameplay
{
    public abstract class BasePlayer : MonoBehaviour
    {
        protected GameManager GameManager { get; private set; }

        private List<GameInput> _replayInputs;

        public IReadOnlyList<GameInput> ReplayInputs => _replayInputs.AsReadOnly();

        public YargPlayer Player;

        protected bool IsReplay      { get; private set; }
        protected bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            _replayInputs = new List<GameInput>();
        }

        protected void Initialize(YargPlayer player)
        {
            Player = player;

            IsReplay = GlobalVariables.Instance.isReplay;
            IsInitialized = true;
        }

        protected abstract void Update();

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
        where TEngine : BaseEngine where TNote : Note<TNote>
    {
        protected TEngine Engine { get; set; }

        protected List<TNote> Notes { get; private set; }

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