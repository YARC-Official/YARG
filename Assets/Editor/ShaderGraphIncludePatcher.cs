using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ShaderGraphIncludePatcher
{
    static ShaderGraphIncludePatcher()
    {
        PatchIncludeCollection();
    }

    static void PatchIncludeCollection()
    {
        try
        {
            // Find the UniversalTarget type
            var shaderGraphAssembly = typeof(UnityEditor.Rendering.Universal.ShaderGraph.MaterialType).Assembly;

            var targetType = shaderGraphAssembly.GetType("UnityEditor.Rendering.Universal.ShaderGraph.CoreIncludes");
            if (targetType == null)
            {
                Debug.LogError("UniversalTarget type not found");
                return;
            }

            var field = targetType.GetField("CorePostgraph", BindingFlags.Public | BindingFlags.Static);
            if (field == null)
            {
                Debug.LogError("CorePostgraph field not found");
                return;
            }

            // Get the IncludeCollection instance
            var includeCollection = field.GetValue(null);

            // Get the internal list
            var listField = includeCollection.GetType().GetField("includes", BindingFlags.NonPublic | BindingFlags.Instance);
            if (listField == null)
            {
                Debug.LogError("IncludeCollection.includes field not found");
                return;
            }

            var includes = listField.GetValue(includeCollection) as System.Collections.IList;
            if (includes == null)
            {
                Debug.LogError("Includes list is null");
                return;
            }

            // Modify the second entry (kVaryings)
            if (includes.Count > 1)
            {
                var includeType = includes[1].GetType();
                var guid = AssetDatabase.AssetPathToGUID("Assets/Art/Shaders/ShaderGraph/Includes/Varyings.hlsl");
                var guidField = includeType.GetField("_guid", BindingFlags.NonPublic | BindingFlags.Instance);
                if (guidField != null)
                {
                    guidField.SetValue(includes[1], guid);
                    Debug.Log("✅ Patched CorePostgraph to use custom Varyings.hlsl");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("ShaderGraph patch failed: " + ex);
        }
    }
}
