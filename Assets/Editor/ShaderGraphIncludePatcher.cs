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

            var guid = AssetDatabase.AssetPathToGUID("Assets/Art/Shaders/ShaderGraph/Includes/Varyings.hlsl");

            foreach (var f in targetType.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (f.FieldType.Name != "IncludeCollection")
                {
                    continue;
                }
                var value = f.GetValue(null);

                if (value != null)
                {
                    // Get the internal list
                    var listField = value.GetType().GetField("includes", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (listField == null)
                    {
                        Debug.LogError("IncludeCollection.includes field not found");
                        continue;
                    }

                    var includes = listField.GetValue(value) as System.Collections.IList;
                    if (includes == null)
                    {
                        Debug.LogError("Includes list is null");
                        continue;
                    }

                    foreach (var include in includes)
                    {
                        var includeType = include.GetType();
                        var guidField = includeType.GetField("_guid", BindingFlags.NonPublic | BindingFlags.Instance);
                        var pathField = includeType.GetField("_path", BindingFlags.NonPublic | BindingFlags.Instance);
                        Debug.Assert(guidField != null && pathField != null);
                        if (pathField.GetValue(include) as string == "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/Varyings.hlsl")
                        {
                            guidField.SetValue(include, guid);
                            Debug.LogFormat("✅ Patched {0} to use custom Varyings.hlsl", f.Name);
                        }

                    }

                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("ShaderGraph patch failed: " + ex);
        }
    }
}
