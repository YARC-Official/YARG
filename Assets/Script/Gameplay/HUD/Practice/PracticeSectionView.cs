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

        private int _relativeSectionIndex;
        private PracticeSectionMenu _practiceSectionMenu;

        public void Init(int relativeSectionIndex, PracticeSectionMenu practiceSectionMenu)
        {
            _relativeSectionIndex = relativeSectionIndex;
            _practiceSectionMenu = practiceSectionMenu;
        }

        public void UpdateView()
        {
            int hoveredIndex = _practiceSectionMenu.HoveredIndex;
            int realIndex = hoveredIndex + _relativeSectionIndex;

            // Hide if out of range
            if (realIndex < 0 || realIndex >= _practiceSectionMenu.Sections.Count)
            {
                _canvasGroup.alpha = 0f;
                _button.interactable = false;
                return;
            }

            int? firstSelected = _practiceSectionMenu.FirstSelectedIndex;

            bool selected = _relativeSectionIndex == 0;
            bool highlighted = selected || firstSelected == realIndex;
            bool between = false;
            if (firstSelected < hoveredIndex)
            {
                between = realIndex > firstSelected && realIndex < hoveredIndex;
            }
            else if (firstSelected > hoveredIndex)
            {
                between = realIndex > hoveredIndex && realIndex < firstSelected;
            }

            var section = _practiceSectionMenu.Sections[realIndex];

            _canvasGroup.alpha = 1f;
            _button.interactable = true;

            _sectionName.text = section.Name;

            _normalBackground.SetActive(!highlighted && !between);
            _selectedBackground.SetActive(highlighted || between);

            if (highlighted || between)
            {
                _sectionName.text = $"<color=white><font-weight=700>{_sectionName.text}</font-weight></color>";
            }
        }
    }
}