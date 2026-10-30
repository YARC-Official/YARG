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

            var mouse = Mouse.current;

            // Get the mouse position
            var mousePos = mouse?.position.ReadValue() ?? Vector2.zero;
            mousePos = _camera.ScreenToViewportPoint(mousePos);

            // Clamp
            mousePos.x = Mathf.Clamp(mousePos.x, 0f, 1f);
            mousePos.y = Mathf.Clamp(mousePos.y, 0f, 1f);

            // Move camera with the cursor, or hold it in place when there is no mouse
            var followRate = mouse != null ? Time.deltaTime * 8f : 0f;
            var transformCache = _camera.transform;
            var initialPos = transformCache.localPosition;
            transformCache.localPosition = initialPos
                .WithX(Mathf.Lerp(initialPos.x, mousePos.x / 4f, followRate))
                .WithY(Mathf.Lerp(initialPos.y, mousePos.y / 3f - 0.25f, followRate));
        }
    }
}