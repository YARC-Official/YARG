using System.Collections.Generic;
using UnityEngine;

namespace YARG.Venue
{
    public static class AnimatorExtensions
    {
        private static readonly Dictionary<Animator, HashSet<int>> _parameterCache = new();

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

        private static HashSet<int> GetValidParameters(Animator animator)
        {
            _parameterCache.TryGetValue(animator, out var hashes);
            return hashes;
        }

        public static void SafeSetTrigger(this Animator animator, int hash)
        {
            if (GetValidParameters(animator).Contains(hash))
            {
                animator.SetTrigger(hash);
            }
        }

        public static void SafeSetBool(this Animator animator, int hash, bool value)
        {
            if (GetValidParameters(animator).Contains(hash))
            {
                animator.SetBool(hash, value);
            }
        }

        public static void SafeSetFloat(this Animator animator, int hash, float value)
        {
            if (GetValidParameters(animator).Contains(hash))
            {
                animator.SetFloat(hash, value);
            }
        }

        public static void SafeCrossFadeInFixedTime(this Animator animator, int hash, float value, int blendLayer)
        {
            if (GetValidParameters(animator).Contains(hash))
            {
                animator.CrossFadeInFixedTime(hash, value, blendLayer);
            }
        }
    }
}