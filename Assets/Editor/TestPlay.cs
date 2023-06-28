using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;
using YARG.Util;

namespace Editor
{
    [InitializeOnLoad]
    public class TestPlay
    {
        private const string PLAY_INFO_PATH = "Assets/Settings/TestPlayInfo.asset";

        static TestPlay()
        {
            ToolbarExtender.RightToolbarGUI.Add(OnToolbarGUI);

            // Create "TestPlayInfo.asset" if it doesn't exist
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(PLAY_INFO_PATH)))
            {
                var newInfo = ScriptableObject.CreateInstance<TestPlayInfo>();
                AssetDatabase.CreateAsset(newInfo, PLAY_INFO_PATH);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void OnToolbarGUI()
        {
            if (GUILayout.Button(new GUIContent("Test Play",
                "Load into a song with full band bots. The song can be set from in game.")))
            {
                if (!EditorApplication.isPlaying)
                {
                    // Set TestPlayInfo file to test play mode
                    var testPlayInfo = AssetDatabase.LoadAssetAtPath<TestPlayInfo>(PLAY_INFO_PATH);
                    testPlayInfo.TestPlayMode = true;

                    // Enter play mode
                    EditorApplication.EnterPlaymode();
                }
            }

            GUILayout.FlexibleSpace();
        }
    }
}