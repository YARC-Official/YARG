using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Assets.Script.Helpers;

namespace YARG.Gameplay.HUD
{
    public class PracticeSectionView : MonoBehaviour
    {
        [SerializeField]
        private CanvasGroup _canvasGroup;
        [SerializeField]
        private Button _button;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _sectionName;
        [SerializeField]
        private Image _background;

        [Space]
        [SerializeField]
        private Color _normalTextColor;
        [SerializeField]
        private Color _highlightedTextColor;
        [SerializeField]
        private Color _firstBackgroundColor;
        [SerializeField]
        private Color _betweenBackgroundColor;
        [SerializeField]
        private Color _selectedBackgroundColor;

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
            bool isFirst = firstSelected == realIndex;
            bool highlighted = selected || isFirst;

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

            // Set text
            _sectionName.text = PracticeSectionHelper.ParseSectionName(section.Name);
            if (highlighted || between)
            {
                _sectionName.color = _highlightedTextColor;
            }
            else
            {
                _sectionName.color = _normalTextColor;
            }

            // Set background
            if (selected)
            {
                _background.color = _selectedBackgroundColor;
            }
            else if (isFirst)
            {
                _background.color = _firstBackgroundColor;
            }
            else if (between)
            {
                _background.color = _betweenBackgroundColor;
            }
            else
            {
                _background.color = Color.clear;
            }
        }
    }
}
