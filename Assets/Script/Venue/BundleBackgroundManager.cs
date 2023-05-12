using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YARG.Venue {
	public class BundleBackgroundManager : MonoBehaviour {
		[SerializeField]
		private Camera mainCamera;

		private RenderTexture bgTexture;

		[HideInInspector]
		public AssetBundle Bundle { get; set; }

		private void Start() {
			// Move object out of the way just in case

			transform.position += Vector3.up * 1000;
			bgTexture = new RenderTexture(Screen.currentResolution.width, Screen.currentResolution.height, 16, RenderTextureFormat.ARGB32);
			bgTexture.Create();

			mainCamera.targetTexture = bgTexture;

			GameUI.Instance.background.texture = bgTexture;
		}

		private void OnDestroy() {
			bgTexture.Release();
			Bundle.Unload(true);
		}

#if UNITY_EDITOR
		// Code to export a background from the editor
		// This honestly should be on a different class (and ideally on a completely different project as a template) but as a quick dirty PoC it will do for now

		private GameObject tromboneBackground;

		[ContextMenu("Export Background")]
		public void ExportBackground() {
			tromboneBackground = gameObject;
			string path = EditorUtility.SaveFilePanel("Save Background", string.Empty, "bg",
				"yarground");

			BuildTargetGroup selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
			BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

			GameObject clonedTromboneBackground = null;

			try {
				if (!string.IsNullOrEmpty(path)) {
					clonedTromboneBackground = Instantiate(tromboneBackground.gameObject);

					string fileName = Path.GetFileName(path);
					string folderPath = Path.GetDirectoryName(path);

					// serialize tromboners (this one is not unity's fault, it's base game weirdness)
					var trombonePaths = new List<string>() { "Assets/_Background.prefab" };

					PrefabUtility.SaveAsPrefabAsset(clonedTromboneBackground.gameObject, "Assets/_Background.prefab");
					AssetBundleBuild assetBundleBuild = default;
					assetBundleBuild.assetBundleName = fileName;
					assetBundleBuild.assetNames = trombonePaths.ToArray();

					BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
						new AssetBundleBuild[] { assetBundleBuild }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
						EditorUserBuildSettings.activeBuildTarget);
					EditorPrefs.SetString("currentBuildingAssetBundlePath", folderPath);
					EditorUserBuildSettings.SwitchActiveBuildTarget(selectedBuildTargetGroup, activeBuildTarget);

					foreach (var asset in trombonePaths) {
						AssetDatabase.DeleteAsset(asset);
					}

					if (File.Exists(path)) File.Delete(path);

					// Unity seems to save the file in lower case, which is a problem on Linux, as file systems are case sensitive there
					File.Move(Path.Combine(Application.temporaryCachePath, fileName.ToLowerInvariant()), path);

					AssetDatabase.Refresh();

					EditorUtility.DisplayDialog("Export Successful!", "Export Successful!", "OK");

					if (clonedTromboneBackground != null) DestroyImmediate(clonedTromboneBackground);
				}
			} catch {
				if (clonedTromboneBackground != null) DestroyImmediate(clonedTromboneBackground);
			}

		}
#endif
	}
}
