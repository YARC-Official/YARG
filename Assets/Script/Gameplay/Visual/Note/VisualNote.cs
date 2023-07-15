using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay
{
    public abstract class VisualNote<TNote, TPlayer> : MonoBehaviour
        where TNote : Note<TNote>
        where TPlayer : BasePlayer
    {
        protected GameManager GameManager { get; private set;  }
        protected TPlayer Player { get; private set; }

        public TNote NoteRef { get; set; }

        private void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            Player = GetComponentInParent<TPlayer>();
        }

        private void OnEnable()
        {
            if (NoteRef is null) return;

            InitializeNote();

            // Force update the position once just in case to prevent flickering
            Update();
        }

        protected abstract void InitializeNote();

        private void Update()
        {
            float noteSpeed = Player.Player.Profile.NoteSpeed;
            float z = (float) (NoteRef.Time - GameManager.SongTime) * noteSpeed - BasePlayer.STRIKE_LINE_POS;

            transform.localPosition = transform.localPosition.WithZ(z);
        }
    }
}