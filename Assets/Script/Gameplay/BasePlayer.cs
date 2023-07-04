using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Player;

namespace YARG.Gameplay
{
    public abstract class BasePlayer<TEngine, TNote> : MonoBehaviour where TEngine : BaseEngine where TNote : Note<TNote>
    {

        private bool _isInitialized;

        protected GameManager GameManager;

        protected TEngine Engine { get; set; }
        protected YargPlayer Player { get; private set; }

        protected List<TNote> Notes { get; private set; }

        public virtual void Initialize(YargPlayer player, List<TNote> notes)
        {
            if(_isInitialized)
            {
                return;
            }

            Player = player;
            Notes = notes;
        }

        protected virtual void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
        }

        protected void Start()
        {
            SubscribeToInputEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeFromInputEvents();
        }

        protected abstract void Update();

        protected abstract void SubscribeToInputEvents();
        protected abstract void UnsubscribeFromInputEvents();
    }
}