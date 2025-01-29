using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Extensions;
using YARG.Localization;

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
            _sectionName.text = ParseSectionName(section.Name);
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

        private string ParseSectionName(string sectionName)
        {
            // Handle letter-based sections like "A section" (prc_a) and "B section 3" (prc_b3)
            if (IsLetterBasedSectionName(sectionName))
            {
                var letterBasedName = Localize.KeyFormat(
                    ("Gameplay", "Practice", "SectionFormats", "LetterSection"),
                    char.ToUpper(sectionName[0])
                );

                if (sectionName.Length == 1)
                {
                    return letterBasedName;
                }

                return Localize.KeyFormat(
                    ("Gameplay", "Practice", "SectionFormats" , "WithNumber"),
                    letterBasedName,
                    sectionName[1..]
                );
            }

            (var name, var number) = DeriveNameAndNumber(sectionName);

            var key = Localize.MakeKey("Gameplay", "Practice", "Sections", name);
            var localizedName = Localize.Key(key);

            if (localizedName == key)
            {
                // No localization for the section name, so return unlocalized
                return sectionName.Replace('_', ' ');
            }

            if (number != null)
            {
                return Localize.KeyFormat(("Gameplay", "Practice", "SectionFormats", "WithNumber"), localizedName, number);
            }

            return localizedName;
        }

        // Whether the string follows the convention for letter-based section names
        // e.g., "a" for "A section", "b3" for "B section 3", or "z92b" for "Z section 92B"
        private bool IsLetterBasedSectionName(string text)
        {
            if (text.Length == 0)
            {
                return false;
            }

            if (text.Length == 1)
            {
                return text[0].IsAsciiLetterLower();
            }

            return text[0].IsAsciiLetterLower() && text[1].IsAsciiDigit() && IsSectionNumber(text[1..]);
        }

        private (string name, string number) DeriveNameAndNumber(string section)
        {
            if (section.Contains('_') && !section.EndsWith('_'))
            {
                var indexOfLastUnderscore = section.LastIndexOf('_');
                var lastWord = section[(indexOfLastUnderscore + 1)..];
                if (IsSectionNumber(lastWord))
                {
                    var name = section[0..indexOfLastUnderscore];

                    string normalizedSectionName;
                    return (alternateSectionNames.TryGetValue(name, out normalizedSectionName) ? normalizedSectionName : name, lastWord);
                }
            }

            string normalizedName;
            return (alternateSectionNames.TryGetValue(section, out normalizedName) ? normalizedName : section, null);
        }

        // Whether the string follows the convention for section numbers (a, 1, 1a)
        private bool IsSectionNumber(string text)
        {
            if (text.Length == 0)
            {
                return false;
            }

            if (text.Length == 1)
            {
                return text[0].IsAsciiLetterLower() || text[0].IsAsciiDigit();
            }

            return text[..^2].All(c => c.IsAsciiDigit()) && (text[^1].IsAsciiLetterLower() || text[^1].IsAsciiDigit());
        }

        private Dictionary<string, string> alternateSectionNames = new()
        {
            { "syth_enters", "synth_enters" },
            { "keyb_enters", "keyboard_enters" },
            { "perc_solo", "percussion_solo" },
            { "sctrach_break", "scratch_break"}
        };
    }
}