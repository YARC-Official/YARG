using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Venue
{
    public static class AnimatorExtensions
    {
        private static readonly Dictionary<Animator, HashSet<int>> _parameterCache = new();
        private static readonly Dictionary<Animator, HashSet<int>> _stateCache     = new();

        /// <summary>
        /// Clears the parameter cache. Should be called when the venue is unloaded.
        /// </summary>
        public static void ClearParameterCache()
        {
            _parameterCache.Clear();
        }

        /// <summary>
        /// Pre-caches the parameter hashes for an animator.
        /// This should be called during initialization to avoid overhead during gameplay.
        /// </summary>
        public static void RegisterAnimator(Animator animator)
        {
            if (animator == null || _parameterCache.ContainsKey(animator))
            {
                return;
            }

            var hashes = new HashSet<int>();
            int count = animator.parameterCount;
            for (int i = 0; i < count; i++)
            {
                hashes.Add(animator.GetParameter(i).nameHash);
            }
            _parameterCache[animator] = hashes;
        }

        /// <summary>
        /// Pre-caches hashes to be used with CrossFadeInFixedTime.
        /// Call this after RegisterAnimator, once per layer for any Animators that will
        /// be called with Blend commands.
        ///
        /// This is separate from RegisterAnimator
        /// because these states require a layer name in addition to the state name.
        /// </summary>
        public static void RegisterBlendStates(Animator animator, int layerIndex,
            IEnumerable<int> qualifiedHashes)
        {
            if (animator == null || _stateCache.ContainsKey(animator))
            {
                return;
            }

            var stateHashes = new HashSet<int>();
            foreach (var hash in qualifiedHashes)
            {
                if (animator.HasState(layerIndex, hash))
                {
                    stateHashes.Add(hash);
                }
            }
            _stateCache[animator] = stateHashes;
        }

        private static bool IsValidParameter(this Animator animator, int hash)
        {
            _parameterCache.TryGetValue(animator, out var hashes);
            return hashes != null && hashes.Contains(hash);
        }

        private static bool IsValidBlendParameter(this Animator animator, int hash)
        {
            _stateCache.TryGetValue(animator, out var hashes);
            return hashes != null && hashes.Contains(hash);
        }

        public static void SafeSetTrigger(this Animator animator, int hash)
        {
            if (animator.IsValidParameter(hash))
            {
                animator.SetTrigger(hash);
            }
        }

        public static void SafeSetBool(this Animator animator, int hash, bool value)
        {
            if (animator.IsValidParameter(hash))
            {
                animator.SetBool(hash, value);
            }
        }

        public static void SafeSetFloat(this Animator animator, int hash, float value)
        {
            if (animator.IsValidParameter(hash))
            {
                animator.SetFloat(hash, value);
            }
        }

        public static void SafeCrossFadeInFixedTime(this Animator animator, int hash, float value, int blendLayer)
        {
            if (animator.IsValidBlendParameter(hash))
            {
                if (blendLayer == -1)
                {
                    animator.CrossFadeInFixedTime(hash, value);
                    return;
                }

                animator.CrossFadeInFixedTime(hash, value, blendLayer);
            }
        }
    }
}