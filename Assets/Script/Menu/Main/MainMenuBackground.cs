using UnityEngine;
using UnityEngine.InputSystem;

namespace YARG.Menu.Main
{
    public class MainMenuBackground : MonoBehaviour
    {
        [SerializeField]
        private Transform _cameraContainer;
        [SerializeField]
        private Camera _camera;

        private void Start()
        {
            _cameraContainer.transform.position = new Vector3(0, 2f, 0);
        }

        private void Update()
        {
            // Move the camera container down
            _cameraContainer.transform.position = Vector3.Lerp(_cameraContainer.transform.position,
                new Vector3(0, 0.5f, 0), Time.deltaTime * 1.5f);

            // Get the mouse position
            var mousePos = Mouse.current.position.ReadValue();
            mousePos = _camera.ScreenToViewportPoint(mousePos);

            // Clamp
            mousePos.x = Mathf.Clamp(mousePos.x, 0f, 1f);
            mousePos.y = Mathf.Clamp(mousePos.y, 0f, 1f);

            // Move camera with the cursor
            var transformCache = _camera.transform;
            var initialPos = transformCache.localPosition;
            transformCache.localPosition = initialPos
                .WithX(Mathf.Lerp(initialPos.x, mousePos.x / 4f, Time.deltaTime * 8f))
                .WithY(Mathf.Lerp(initialPos.y, mousePos.y / 3f - 0.25f, Time.deltaTime * 8f));
        }
    }
}