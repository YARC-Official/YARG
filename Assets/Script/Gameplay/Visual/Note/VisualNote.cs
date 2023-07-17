using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public abstract class VisualNote<TNote, TPlayer> : MonoBehaviour, IPoolable
        where TNote : Note<TNote>
        where TPlayer : BasePlayer
    {
        // TODO: Migrate this to using ECS DOTS or something to speed it up.

        private const float REMOVE_POINT = -4f;

        protected GameManager GameManager { get; private set;  }
        protected TPlayer Player { get; private set; }
        public Pool ParentPool { get; set; }

        public TNote NoteRef { get; set; }

        private void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            Player = GetComponentInParent<TPlayer>();
        }

        public void EnableFromPool()
        {
            gameObject.SetActive(true);

            InitializeNote();

            // Force update the position once just in case to prevent flickering
            Update();
        }

        protected abstract void InitializeNote();

        private void Update()
        {
            float noteSpeed = Player.Player.Profile.NoteSpeed;

            // TODO: Take calibration into consideration
            float z = BasePlayer.STRIKE_LINE_POS + (float) (NoteRef.Time - GameManager.RealSongTime) * noteSpeed;

            transform.localPosition = transform.localPosition.WithZ(z);

            if (z < REMOVE_POINT || NoteRef.WasHit || NoteRef.WasMissed)
            {
                ParentPool.Return(this);
            }
        }

        public void DisableIntoPool()
        {
            gameObject.SetActive(false);
        }
    }
}