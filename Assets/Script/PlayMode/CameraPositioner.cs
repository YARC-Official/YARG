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
			// FOV
			GetComponent<Camera>().fieldOfView = SettingsManager.Settings.TrackCamFOV.Data;

			// Z Pos
			float zOffset = SettingsManager.Settings.TrackCamZPos.Data;
			zOffset -= 4f;

			// Orbit (position)
			var radians = SettingsManager.Settings.TrackCamOrbit.Data * Mathf.Deg2Rad;
			var position = new Vector3(
				0f,
				Mathf.Sin(radians) * 4.86f,
				Mathf.Cos(radians) * 4.86f + zOffset
			);
			transform.localPosition = position;

			// Orbit (rotation)
			transform.LookAt(transform.parent.position
				.AddZ(zOffset)
				.AddZ(SettingsManager.Settings.TrackCamRot.Data - 4f));
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