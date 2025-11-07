using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace YARG.Menu.Main
{
    public class MainMenuBackground : MonoBehaviour
    {
        [SerializeField]
        private Transform _cameraContainer;
        [SerializeField]
        private Camera _camera;

        private bool _disabledForHeadless;

        private void Awake()
        {
            // Dedicated/headless builds do not have cameras or input devices.
            _disabledForHeadless = Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
        }

        private void Start()
        {
            if (_disabledForHeadless)
            {
                enabled = false;
                return;
            }

            if (_cameraContainer == null || _camera == null)
            {
                Debug.LogWarning("[MainMenuBackground] Missing camera references. Disabling parallax background.");
                enabled = false;
                return;
            }

            _cameraContainer.transform.position = new Vector3(0, 2f, 0);
        }

        private void Update()
        {
            if (_disabledForHeadless)
            {
                return;
            }

            // Get the mouse position
            if (Mouse.current == null)
            {
                return;
            }

            // Move the camera container down
            _cameraContainer.transform.position = Vector3.Lerp(_cameraContainer.transform.position,
                new Vector3(0, 0.5f, 0), Time.deltaTime * 1.5f);

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