using System.Linq;
using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class AnimatorExtensions
    {
        public static bool HasParameter(this Animator animator, string param)
        {
            return animator.parameters.Any(i => i.name == param);
        }

        public static bool HasParameter(this Animator animator, int hash)
        {
            return animator.parameters.Any(i => i.nameHash == hash);
        }
    }
}