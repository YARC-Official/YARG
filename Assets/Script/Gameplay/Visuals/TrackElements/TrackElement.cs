using UnityEngine;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public abstract class TrackElement<TPlayer> : MonoBehaviour, IPoolable
        where TPlayer : BasePlayer
    {
        protected const float REMOVE_POINT = -4f;

        protected GameManager GameManager { get; private set;  }
        protected TPlayer Player { get; private set; }
        public Pool ParentPool { get; set; }

        protected abstract double ElementTime { get; }

        protected bool Initialized { get; private set; }

        private void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            Player = GetComponentInParent<TPlayer>();

            // Hide everything at the start
            HideElement();
        }

        public void EnableFromPool()
        {
            gameObject.SetActive(true);

            InitializeElement();
            Initialized = true;

            // Force update the position once just in case to prevent flickering
            Update();
        }

        protected abstract void InitializeElement();
        protected abstract void HideElement();
        protected abstract void UpdateElement();

        protected void Update()
        {
            // Skip if not initialized
            if (!Initialized) return;

            float noteSpeed = Player.Player.Profile.NoteSpeed;

            // TODO: Take calibration into consideration
            float z =
                BasePlayer.STRIKE_LINE_POS                     // Shift origin to the strike line
                + (float) (ElementTime - GameManager.SongTime) // Get time of note relative to now
                * noteSpeed;                                   // Adjust speed (units/s)

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithZ(z);

            if (z < REMOVE_POINT)
            {
                ParentPool.Return(this);
                return;
            }

            UpdateElement();
        }

        public void DisableIntoPool()
        {
            HideElement();

            Initialized = false;
            gameObject.SetActive(false);
        }
    }
}