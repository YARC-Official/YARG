using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YARG.Venue;

namespace Editor {
	[CustomEditor(typeof(BundleBackgroundManager))]
	public class BundleBackgroundInspector : UnityEditor.Editor {
		private SerializedProperty _mainCameraProperty;

		private void OnEnable() {
			_mainCameraProperty = serializedObject.FindProperty("mainCamera");
		}


		public override VisualElement CreateInspectorGUI() {
			// Create a new VisualElement to be the root of our inspector UI
			var myInspector = new VisualElement();

			myInspector.Add(new Label("\n<b><size=1.25em>Bundle Background Manager (IMPORTANT)</size></b>\n"));
			myInspector.Add(new Label("This is the window you can use to manage and export your venue/background! " +
			                          "If you have any issues at all creating a venue, please feel free to reach out " +
			                          "on our Discord.\n\nThe below property contains a reference to the <b>main</b> camera.") {
				style = {
					whiteSpace = WhiteSpace.Normal
				}
			});

			myInspector.Add(new PropertyField(_mainCameraProperty));

			myInspector.Add(new Label("\n\n<b><size=1.25em>Actions</size></b>\n"));

			// "Export Background" button
			var exportButton = new Button(() => {
				if (target is BundleBackgroundManager manager) {
					manager.ExportBackground();
				}
			});
			exportButton.Add(new Label("Export Background"));
			myInspector.Add(exportButton);

			return myInspector;
		}
	}
}