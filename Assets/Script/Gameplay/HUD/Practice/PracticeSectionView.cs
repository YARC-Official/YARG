using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class PracticeSectionView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;
        [SerializeField]
        private Button _button;
        [SerializeField]
        private GameObject _normalBackground;
        [SerializeField]
        private GameObject _selectedBackground;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _sectionName;

        private Section _section;

        public void ShowAsSection(Section section)
        {
            _canvasGroup.alpha = 1f;
            _button.interactable = true;

            _section = section;

            _sectionName.text = _section.Name;

            _normalBackground.SetActive(true);
            _selectedBackground.SetActive(false);

            // if (selected)
            // {
            //     _sectionName.text = $"<color=white><font-weight=700>{_sectionName.text}</font-weight></color>";
            // }
        }

        public void Hide()
        {
            _canvasGroup.alpha = 0f;
            _button.interactable = false;

            _section = null;
        }
    }
}