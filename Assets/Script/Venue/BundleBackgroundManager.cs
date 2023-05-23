using System.IO;
using UnityEngine;
using YARG.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YARG.Venue {
	public class BundleBackgroundManager : MonoBehaviour {
		// DO NOT CHANGE THIS! It will break existing venues
		public const string BackgroundPrefabPath = "Assets/_Background.prefab";

		[SerializeField]
		private Camera mainCamera;

		private RenderTexture bgTexture;

		[HideInInspector]
		public AssetBundle Bundle { get; set; }

		private void Start() {
			// Move object out of the way just in case

			transform.position += Vector3.up * 1000f;
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

		// 
		// HUGE thanks to the people over at Trombone Champ and NyxTheShield for giving us this code.
		// This could not be done without them.
		// 
		// Code to export a background from the editor.
		//

		private GameObject backgroundReference;

		[ContextMenu("Export Background")]
		public void ExportBackground() {
			backgroundReference = gameObject;
			string path = EditorUtility.SaveFilePanel("Save Background", string.Empty, "bg",
				"yarground");

			BuildTargetGroup selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
			BuildTarget activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

			GameObject clonedBackground = null;

			try {
				if (string.IsNullOrEmpty(path)) {
					return;
				}

				clonedBackground = Instantiate(backgroundReference.gameObject);

				string fileName = Path.GetFileName(path);
				string folderPath = Path.GetDirectoryName(path);

				var assetPaths = new string[] {
						BackgroundPrefabPath
					};

				PrefabUtility.SaveAsPrefabAsset(clonedBackground.gameObject, BackgroundPrefabPath);
				AssetBundleBuild assetBundleBuild = default;
				assetBundleBuild.assetBundleName = fileName;
				assetBundleBuild.assetNames = assetPaths;

				BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
					new AssetBundleBuild[] { assetBundleBuild }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
					EditorUserBuildSettings.activeBuildTarget);
				EditorPrefs.SetString("currentBuildingAssetBundlePath", folderPath);
				EditorUserBuildSettings.SwitchActiveBuildTarget(selectedBuildTargetGroup, activeBuildTarget);

				foreach (var asset in assetPaths) {
					AssetDatabase.DeleteAsset(asset);
				}

				// If the file exists, delete it (to replace it)
				if (File.Exists(path)) {
					File.Delete(path);
				}

				// Unity seems to save the file in lower case, which is a problem on Linux, as file systems are case sensitive there
				File.Move(Path.Combine(Application.temporaryCachePath, fileName.ToLowerInvariant()), path);

				AssetDatabase.Refresh();

				EditorUtility.DisplayDialog("Export Successful!", "Export Successful!", "OK");
			} catch (System.Exception e) {
				Debug.LogError("Failed to bundle background/venue.");
				Debug.LogException(e);
			} finally {
				if (clonedBackground != null) {
					DestroyImmediate(clonedBackground);
				}
			}
		}
#endif
	}
}
