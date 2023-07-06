using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Player;

namespace YARG.Gameplay
{
    public abstract class BasePlayer : MonoBehaviour
    {
        protected GameManager GameManager { get; private set; }

        public YargPlayer Player;

        protected bool IsInitialized { get; private set; }

        protected virtual void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
        }

        protected void Initialize(YargPlayer player)
        {
            Player = player;

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

        protected abstract void SubscribeToInputEvents();
        protected abstract void UnsubscribeFromInputEvents();
    }

    public abstract class BasePlayer<TEngine, TNote> : BasePlayer
        where TEngine : BaseEngine where TNote : Note<TNote>
    {
        protected TEngine    Engine { get; set; }

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