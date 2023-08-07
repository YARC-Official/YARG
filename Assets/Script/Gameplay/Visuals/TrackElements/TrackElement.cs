using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public abstract class TrackElement<TPlayer> : MonoBehaviour, IPoolable
        where TPlayer : BasePlayer
    {
        // TODO: We should probably move these somewhere else
        private static readonly int _fadeZeroPosition = Shader.PropertyToID("_FadeZeroPosition");
        private static readonly int _fadeFullPosition = Shader.PropertyToID("_FadeFullPosition");

        protected const float REMOVE_POINT = -4f;

        protected GameManager GameManager { get; private set;  }
        protected TPlayer Player { get; private set; }
        public Pool ParentPool { get; set; }

        /// <summary>
        /// The time at which the element resides.
        /// </summary>
        protected abstract double ElementTime { get; }
        /// <summary>
        /// How many move units the element should be kept for past the <see cref="REMOVE_POINT"/>.
        /// Should be positive.
        /// </summary>
        protected virtual float RemovePointOffset => 0f;
        /// <summary>
        /// The lefty flip position multiplier. <c>1</c> if lefty flip is off, <c>-1</c> if it is on.
        /// This is not automatically accounted for.
        /// </summary>
        protected float LeftyFlipMultiplier => Player.Player.Profile.LeftyFlip ? -1f : 1f;

        protected bool Initialized { get; private set; }

        private void Awake()
        {
            GameManager = FindObjectOfType<GameManager>();
            Player = GetComponentInParent<TPlayer>();

            // Hide everything at the start
            HideElement();

            // Get fade info
            // TODO: Make this per player. This is why we don't use global shader values
            float fadePos = SettingsManager.Settings.TrackFadePosition.Data;
            float fadeSize = SettingsManager.Settings.TrackFadeSize.Data;

            // Set all fade values
            var meshRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var meshRenderer in meshRenderers)
            {
                foreach (var material in meshRenderer.materials)
                {
                    material.SetVector(_fadeZeroPosition, new Vector4(0f, 0f, fadePos, 0f));
                    material.SetVector(_fadeFullPosition, new Vector4(0f, 0f, fadePos - fadeSize, 0f));
                }
            }
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

            // TODO: Take calibration into consideration
            float z =
                BasePlayer.STRIKE_LINE_POS                     // Shift origin to the strike line
                + (float) (ElementTime - GameManager.SongTime) // Get time of note relative to now
                * Player.NoteSpeed;                            // Adjust speed (units/s)

            var cacheTransform = transform;
            cacheTransform.localPosition = cacheTransform.localPosition.WithZ(z);

            if (z < REMOVE_POINT - RemovePointOffset)
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