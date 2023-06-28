using UnityEditor;
using UnityEditor.SceneManagement;

namespace Editor
{
    [InitializeOnLoad]
    public class StartScene
    {
        static StartScene()
        {
            EditorSceneManager.playModeStartScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(EditorBuildSettings.scenes[0].path);
        }
    }
}