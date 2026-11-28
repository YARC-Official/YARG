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
            if (realIndex < 0 || realIndex >= _practiceSectionMenu.TotalItemCount)
            {
                _canvasGroup.alpha = 0f;
                _button.interactable = false;
                return;
            }

            bool hasRestoreOption = _practiceSectionMenu.HasRestoreOption;
            bool isRestoreRow = hasRestoreOption && realIndex == 0;

            bool selected = _relativeSectionIndex == 0;
            bool isFirst = false;
            bool between = false;

            if (!isRestoreRow)
            {
                int? firstSelected = _practiceSectionMenu.FirstSelectedIndex;
                int? firstSelectedVisual = firstSelected.HasValue
                    ? firstSelected.Value + (hasRestoreOption ? 1 : 0)
                    : null;

                isFirst = firstSelectedVisual == realIndex;

                if (firstSelectedVisual < hoveredIndex)
                {
                    between = realIndex > firstSelectedVisual && realIndex < hoveredIndex;
                }
                else if (firstSelectedVisual > hoveredIndex)
                {
                    between = realIndex > hoveredIndex && realIndex < firstSelectedVisual;
                }
            }

            bool highlighted = selected || isFirst;

            _canvasGroup.alpha = 1f;
            _button.interactable = true;

            // Set text
            if (isRestoreRow)
            {
                // Use Localize.Key("Menu.Practice.RestoreLast") if using YARG.Localization
                _sectionName.text = "Restore Last Session";
            }
            else
            {
                int sectionIndex = hasRestoreOption ? realIndex - 1 : realIndex;
                var section = _practiceSectionMenu.Sections[sectionIndex];
                _sectionName.text = PracticeSectionHelper.ParseSectionName(section.Name);
            }

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
