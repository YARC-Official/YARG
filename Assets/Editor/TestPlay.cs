using UnityEditor;
using UnityEngine;
using UnityToolbarExtender;
using YARG.Util;

namespace Editor {
	[InitializeOnLoad]
	public class TestPlay {
		static TestPlay() {
			ToolbarExtender.RightToolbarGUI.Add(OnToolbarGUI);
		}

		private static void OnToolbarGUI() {
			if (GUILayout.Button(new GUIContent("Test Play",
				    "Load into a song with full band bots. The song can be set from in game.")))  {

				if (!EditorApplication.isPlaying) {
					// Set TestPlayInfo file to test play mode
					var testPlayInfo = AssetDatabase.LoadAssetAtPath<TestPlayInfo>("Assets/Settings/TestPlayInfo.asset");
					testPlayInfo.TestPlayMode = true;

					// Enter play mode
					EditorApplication.EnterPlaymode();
				}
			}

			GUILayout.FlexibleSpace();
		}
	}
}