using UnityEditor;
using UnityEngine;

namespace Editor
{
    public static class FadeMaterialReset
    {
        [MenuItem("YARG/Force Disable Material Fading", false, 1000)]
        public static void DisableMaterialFading()
        {
            Shader.SetGlobalFloat("_IsFading", 0f);
        }
    }
}