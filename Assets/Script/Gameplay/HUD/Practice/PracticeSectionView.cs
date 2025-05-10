using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor.Build.Pipeline;
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
            // Best-effort attempt to convert [section]-based practice sections to the expected format
            sectionName = sectionName.ToLower().Replace(' ', '_').Replace("guitar", "gtr");

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
                    ("Gameplay", "Practice", "SectionFormats", "WithNumber"),
                    letterBasedName,
                    sectionName[1..].TrimStart('_')
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

        /** 
         * Whether the string follows the convention for letter-based section names
         */
        private bool IsLetterBasedSectionName(string text)
        {
            if (text.Length == 0 || !text[0].IsAsciiLetterLower())
            {
                return false;
            }

            if (text.Length == 1)
            {
                // e.g. "a" for "A section"
                return true;
            }

            if (text.Length == 2)
            {
                // e.g. "a1" for "A section 1"
                return text[1].IsAsciiDigit();
            }

            if (text[1] == '_')
            {
                // e.g. "a_b" for "A section B"
                return text.Length == 3 && text[2].IsAsciiLetterLower();
            }

            if (text[1..^1].All(c => c.IsAsciiDigit()))
            {
                if (text[^1].IsAsciiDigit())
                {
                    // e.g. "a123" for "A section 123"
                    return true;
                }

                if (text[^1].IsAsciiLetterLower())
                {
                    // e.g. "a123b" for "A section 123B"
                    return true;
                }
            }

            return false;
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
            { "ah!", "ah" },
            { "big_rock_ending", "bre" },
            { "big_rock_ending!", "bre" },
            { "buildup", "build_up" },
            { "build-up", "build_up" },
            { "fadein", "fade_in" },
            { "fadeout", "fade_out" },
            { "fade-in", "fade_in" },
            { "fade-out", "fade_out" },
            { "high_melody", "hi_melody" },
            { "keyb_enters", "keyboard_enters" },
            { "kick_it!", "kick it" },
            { "low_melody", "lo_melody" },
            { "oohs_and_ahs", "oohs" },
            { "perc_solo", "percussion_solo" },
            { "pre_chorus", "prechorus" },
            { "pre-chorus", "prechorus" },
            { "pre_verse", "preverse" },
            { "pre-verse", "preverse" },
            { "sctrach_break", "scratch_break"},
            { "syth_enters", "synth_enters" },
            { "yeah!", "yeah" }

        };
    }
}