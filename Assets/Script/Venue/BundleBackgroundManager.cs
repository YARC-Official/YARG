using System;
using System.IO;
using UnityEngine;
using YARG.PlayMode;
using YARG.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace YARG.Venue {
	public class BundleBackgroundManager : MonoBehaviour {
		// DO NOT CHANGE THIS! It will break existing venues
		public const string BACKGROUND_PREFAB_PATH = "Assets/_Background.prefab";

		// DO NOT CHANGE the name of this! I *know* it doesn't follow naming conventions, but it will also break existing
		// venues if we do change it.
		//
		// ReSharper disable once InconsistentNaming
		[SerializeField]
		private Camera mainCamera;

		public AssetBundle Bundle { get; set; }

		private void Awake() {
			// Move object out of the way, so its effects don't collide with the tracks
			transform.position += Vector3.forward * 10_000f;

			// Destroy the default camera (venue has its own)
			Destroy(Play.Instance.DefaultCamera.gameObject);
		}

		private void OnDestroy() {
			Bundle.Unload(true);
		}

#if UNITY_EDITOR

		//
		// HUGE thanks to the people over at Trombone Champ and NyxTheShield for giving us this code.
		// This could not be done without them.
		//
		// Code to export a background from the editor.
		//

		private GameObject _backgroundReference;

		[ContextMenu("Export Background")]
		public void ExportBackground() {
			_backgroundReference = gameObject;
			string path = EditorUtility.SaveFilePanel("Save Background", string.Empty, "bg", "yarground");

			var selectedBuildTargetGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
			var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;

			GameObject clonedBackground = null;

			try {
				if (string.IsNullOrEmpty(path)) {
					return;
				}

				clonedBackground = Instantiate(_backgroundReference.gameObject);

				string fileName = Path.GetFileName(path);
				string folderPath = Path.GetDirectoryName(path);

				var assetPaths = new[] {
					BACKGROUND_PREFAB_PATH
				};

				PrefabUtility.SaveAsPrefabAsset(clonedBackground.gameObject, BACKGROUND_PREFAB_PATH);
				AssetBundleBuild assetBundleBuild = default;
				assetBundleBuild.assetBundleName = fileName;
				assetBundleBuild.assetNames = assetPaths;

				BuildPipeline.BuildAssetBundles(Application.temporaryCachePath,
					new[] { assetBundleBuild }, BuildAssetBundleOptions.ForceRebuildAssetBundle,
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
			} catch (Exception e) {
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
