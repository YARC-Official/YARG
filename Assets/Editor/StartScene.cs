using UnityEditor;
using UnityEditor.SceneManagement;

namespace Mechacell.UnityEditorTools {
	[InitializeOnLoad]
	public class StartScene {
		static StartScene() {
			EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorBuildSettings.scenes[0].path);
		}
	}
}