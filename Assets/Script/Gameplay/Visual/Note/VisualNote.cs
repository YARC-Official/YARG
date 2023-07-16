using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public abstract class VisualNote<TNote, TPlayer> : MonoBehaviour, IPoolable
        where TNote : Note<TNote>
        where TPlayer : BasePlayer
    {
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

            float z = (float) (NoteRef.Time - GameManager.SongTime) * noteSpeed - BasePlayer.STRIKE_LINE_POS;
            z += BasePlayer.STRIKE_LINE_POS;

            transform.localPosition = transform.localPosition.WithZ(z);

            if (z < REMOVE_POINT)
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