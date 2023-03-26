using UnityEngine;
using UnityEngine.InputSystem;

namespace YARG {
	public class MainMenuBackground : MonoBehaviour {
		[SerializeField]
		private Transform cameraContainer;
		[SerializeField]
		private new Camera camera;

		private void Start() {
			cameraContainer.transform.position = new Vector3(0, 2f, 0);
		}

		private void Update() {
			// Move the camera container down
			cameraContainer.transform.position = Vector3.Lerp(cameraContainer.transform.position,
				new Vector3(0, 0.5f, 0), Time.deltaTime * 1.5f);

			// Get the mouse position
			var mousePos = Mouse.current.position.ReadValue();
			mousePos = camera.ScreenToViewportPoint(mousePos);

			// Clamp
			mousePos.x = Mathf.Clamp(mousePos.x, 0f, 1f);
			mousePos.y = Mathf.Clamp(mousePos.y, 0f, 1f);

			// Move camera with the cursor
			camera.transform.localPosition = camera.transform.localPosition
				.WithX(Mathf.Lerp(camera.transform.localPosition.x, mousePos.x / 4f, Time.deltaTime * 8f))
				.WithY(Mathf.Lerp(camera.transform.localPosition.y, mousePos.y / 3f - 0.25f, Time.deltaTime * 8f));
		}
	}
}