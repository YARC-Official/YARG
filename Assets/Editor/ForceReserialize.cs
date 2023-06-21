using UnityEditor;
using UnityEngine;

namespace Editor {
	public static class ForceReserialize {
		[MenuItem("Assets/Force Reserialize", false, 23)]
		public static void ForceReserializeSelected() {
			var asset = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]));
			if (asset == null) {
				return;
			}

			EditorUtility.SetDirty(asset);
			AssetDatabase.SaveAssets();
		}

		[MenuItem("Assets/Force Reserialize", true)]
		public static bool ForceReserializeSelectedValidate() {
			return Selection.assetGUIDs.Length >= 1;
		}
	}
}