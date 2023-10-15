using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public abstract class TrackElement<TPlayer> : GameplayBehaviour, IPoolable
        where TPlayer : BasePlayer
    {
        protected const float REMOVE_POINT = -4f;

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

        protected override void GameplayAwake()
        {
            Player = GetComponentInParent<TPlayer>();

            // Hide everything at the start
            HideElement();
        }

        private void Start()
        {
            // Get fade info
            float fadePos = Player.ZeroFadePosition;
            float fadeSize = Player.FadeSize;

            // Set all fade values
            var meshRenderers = GetComponentsInChildren<MeshRenderer>(true);
            foreach (var meshRenderer in meshRenderers)
            {
                foreach (var material in meshRenderer.materials)
                {
                    material.SetFade(fadePos, fadeSize);
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

            // Calibration is not taken into consideration here, as that is instead handled in more
            // critical areas such as the game manager and players
            float z =
                BasePlayer.STRIKE_LINE_POS                          // Shift origin to the strike line
                + (float) (ElementTime - GameManager.RealInputTime) // Get time of note relative to now
                * Player.NoteSpeed;                                 // Adjust speed (units/s)

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static float GetElementX(int index, int subdivisions)
        {
            return BasePlayer.TRACK_WIDTH / subdivisions * index - BasePlayer.TRACK_WIDTH / 2f - 1f / subdivisions;
        }
    }
}