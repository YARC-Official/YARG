using UnityEngine;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    public class DraggingDisplay : MonoBehaviour
    {
        public DraggableHudElement DraggableHud { get; set; }

        [SerializeField]
        private GameObject _buttonContainer;
        [SerializeField]
        private CanvasGroup _canvasGroup;
        [SerializeField]
        private RectTransform _scaleHandle;

        public RectTransform ScaleHandle => _scaleHandle;

        public bool IsScaleHandleAtPoint(Vector2 screenPoint, Camera camera)
        {
            return _scaleHandle.gameObject.activeInHierarchy &&
                _scaleHandle.ContainsScreenPoint(screenPoint, camera);
        }

        public void Show()
        {
            _canvasGroup.alpha = 1f;
            _buttonContainer.SetActive(true);
            _scaleHandle.gameObject.SetActive(DraggableHud.AllowScaling);
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            _buttonContainer.SetActive(false);
        }

        public void DoneButton()
        {
            DraggableHud.Deselect();
        }


        public void ResetButton()
        {
            DraggableHud.ResetElement();
            DraggableHud.Deselect();
        }
    }
}
