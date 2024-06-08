using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class DraggingDisplay : MonoBehaviour
    {
        public DraggableHudElement DraggableHud { get; set; }

        [SerializeField]
        private GameObject _buttonContainer;
        [SerializeField]
        private CanvasGroup _canvasGroup;

        public void Show()
        {
            _canvasGroup.alpha = 1f;
            _buttonContainer.SetActive(true);
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

        public void RevertButton()
        {
            DraggableHud.RevertElement();
            DraggableHud.Deselect();
        }

        public void ResetButton()
        {
            DraggableHud.ResetElement();
            DraggableHud.Deselect();
        }
    }
}