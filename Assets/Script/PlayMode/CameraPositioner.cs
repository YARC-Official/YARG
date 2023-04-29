using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Settings;

namespace YARG.PlayMode {
	public class CameraPositioner : MonoBehaviour {
		private static List<CameraPositioner> cameraPositioners = new();

		private void Start() {
			cameraPositioners.Add(this);

			UpdateAntiAliasing();
			UpdatePosition();
		}

		private void OnDestroy() {
			cameraPositioners.Remove(this);
		}

		private void UpdateAntiAliasing() {
			// Set anti-aliasing
			var info = GetComponent<UniversalAdditionalCameraData>();
			if (SettingsManager.Settings.LowQuality.Data) {
				info.antialiasing = AntialiasingMode.None;
			} else {
				info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
				info.antialiasingQuality = AntialiasingQuality.Low;
			}
		}

		private void UpdatePosition() {
			// TODO
		}

		public static void UpdateAllAntiAliasing() {
			foreach (var cameraPositioner in cameraPositioners) {
				cameraPositioner.UpdateAntiAliasing();
			}
		}

		public static void UpdateAllPosition() {
			foreach (var cameraPositioner in cameraPositioners) {
				cameraPositioner.UpdatePosition();
			}
		}
	}
}