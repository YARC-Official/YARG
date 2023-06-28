using UnityEngine;
using UnityEngine.InputSystem;

namespace YARG
{
    public class MainMenuBackground : MonoBehaviour
    {
        public static MainMenuBackground Instance { get; private set; }

        public bool cursorMoves = true;

        [SerializeField]
        private Transform cameraContainer;

        [SerializeField]
        private new Camera camera;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            cameraContainer.transform.position = new Vector3(0, 2f, 0);
        }

        private void Update()
        {
            // Move the camera container down
            cameraContainer.transform.position = Vector3.Lerp(cameraContainer.transform.position,
                new Vector3(0, 0.5f, 0), Time.deltaTime * 1.5f);

            Vector2 mousePos;
            if (cursorMoves)
            {
                // Get the mouse position
                mousePos = Mouse.current.position.ReadValue();
                mousePos = camera.ScreenToViewportPoint(mousePos);

                // Clamp
                mousePos.x = Mathf.Clamp(mousePos.x, 0f, 1f);
                mousePos.y = Mathf.Clamp(mousePos.y, 0f, 1f);
            }
            else
            {
                mousePos = new Vector2(0f, 0.5f);
            }

            // Move camera with the cursor
            camera.transform.localPosition = camera.transform.localPosition
                .WithX(Mathf.Lerp(camera.transform.localPosition.x, mousePos.x / 4f, Time.deltaTime * 8f))
                .WithY(Mathf.Lerp(camera.transform.localPosition.y, mousePos.y / 3f - 0.25f, Time.deltaTime * 8f));
        }
    }
}