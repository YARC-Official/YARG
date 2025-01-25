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
            const string SECTION_PREFIX = "section ";
            const string PRC_PREFIX = "prc_";

            if (sectionName.StartsWith(SECTION_PREFIX))
            {
                return sectionName[SECTION_PREFIX.Length..].TrimStart('_').Trim();
            }

            if (sectionName.StartsWith(PRC_PREFIX))
            {
                return ParsePrcSectionName(sectionName[PRC_PREFIX.Length..]);
            }

            return sectionName;
        }

        // Receives a prc-based section name with "prc_" removed
        private string ParsePrcSectionName(string prcSectionName)
        {
            // Handle letter-based sections like "A section" (prc_a) and "B section 3" (prc_b3)
            if (isLetterBasedSectionName(prcSectionName))
            {
                var name = char.ToUpper(prcSectionName[0]) + " " + Localize.Key("Gameplay.PracticeSections.Section");
                return prcSectionName.Length == 1 ? name : (name + " " + prcSectionName[1..]);
            }

            foreach (var key in prcNameToLocalizationKey.Keys)
            {
                if (key == prcSectionName)
                {
                    return Localize.Key("Gameplay.PracticeSections." + prcNameToLocalizationKey[key]);
                }

                if (prcSectionName.StartsWith(key))
                {
                    var remainder = prcSectionName[(key.Length + "_".Length)..];

                    if (isSectionNumber(remainder))
                    {
                        return Localize.Key("Gameplay.PracticeSections." + prcNameToLocalizationKey[key]) + " " + remainder;
                    }
                }
            }

            // Not a standard section name, so return unlocalized
            return prcSectionName.Replace('_', ' ').Trim();
        }

        // Whether the string follows the convention for letter-based section names
        // e.g., "a" for "A section", "b3" for "B section 3", or "z92b" for "Z section 92B"
        private bool isLetterBasedSectionName(string text)
        {
            if (text.Length == 0)
            {
                return false;
            }

            if (text.Length == 1)
            {
                return text[0].IsAsciiLetterLower();
            }

            return text[0].IsAsciiLetterLower() && text[1].IsAsciiDigit() && isSectionNumber(text[1..]);
        }

        // Whether the string follows the convention for section numbers (a, 1, 1a)
        private bool isSectionNumber(string text)
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

        private Dictionary<string, string> prcNameToLocalizationKey = new()
        {
            {"intro", "Intro"},
            {"intro_slow", "IntroSlow"},
            {"intro_fast", "IntroFast"},
            {"intro_heavy", "IntroHeavy"},
            {"quiet_intro", "QuietIntro"},
            {"noise_intro", "NoiseIntro"},
            {"drum_intro", "DrumIntro"},
            {"bass_intro", "BassIntro"},
            {"vocal_intro", "VocalIntro"},
            {"gtr_intro", "GtrIntro"},
            {"violin_intro", "ViolinIntro"},
            {"strings_intro", "StringsIntro"},
            {"orch_intro", "OrchIntro"},
            {"horn_intro", "HornIntro"},
            {"harmonica_intro", "HarmonicaIntro"},
            {"organ_intro", "OrganIntro"},
            {"piano_intro", "PianoIntro"},
            {"keyboard_intro", "KeyboardIntro"},
            {"dj_intro", "DJIntro"},
            {"intro_hook", "IntroHook"},
            {"intro_riff", "IntroRiff"},
            {"fade_in", "FadeIn"},
            {"drums_enter", "DrumsEnter"},
            {"bass_enters", "BassEnters"},
            {"gtr_enters", "GtrEnters"},
            {"rhy_enters", "Gtr2Enters"},
            {"band_enters", "BandEnters"},
            {"syth_enters", "SynthEnters"},
            {"keyb_enters", "KeyboardEnters"},
            {"keyboard_enters", "KeyboardEnters"},
            {"organ_enters", "OrganEnters"},
            {"piano_enters", "PianoEnters"},
            {"kick_it", "KickIt"},
            {"intro_verse", "IntroVerse"},
            {"intro_chorus", "IntroChorus"},
            {"verse", "Verse"},
            {"alt_verse", "AltVerse"},
            {"quiet_verse", "QuietVerse"},
            {"preverse", "PreVerse"},
            {"postverse", "PostVerse"},
            {"chorus", "Chorus"},
            {"chorus_break", "ChorusBreak"},
            {"breakdown_chorus", "BreakdownChorus"},
            {"alt_chorus", "AltChorus"},
            {"prechorus", "PreChorus"},
            {"postchorus", "PostChorus"},
            {"bridge", "Bridge"},
            {"gtr_solo", "GtrSolo"},
            {"slide_solo", "SlideSolo"},
            {"drum_solo", "DrumSolo"},
            {"perc_solo", "PercussionSolo"},
            {"bass_solo", "BassSolo"},
            {"organ_solo", "OrganSolo"},
            {"piano_solo", "PianoSolo"},
            {"keyboard_solo", "KeyboardSolo"},
            {"synth_solo", "SynthSolo"},
            {"harmonica_solo", "HarmonicaSolo"},
            {"sax_solo", "SaxSolo"},
            {"horn_solo", "HornSolo"},
            {"flute_solo", "FluteSolo"},
            {"noise_solo", "NoiseSolo"},
            {"dj_solo", "DJSolo"},
            {"slow_part", "SlowPart"},
            {"fast_part", "FastPart"},
            {"quiet_part", "QuietPart"},
            {"loud_part", "LoudPart"},
            {"heavy_part", "HeavyPart"},
            {"spacey", "SpaceyPart"},
            {"spacey_part", "SpaceyPart"},
            {"trippy_part", "TrippyPart"},
            {"break", "Break"},
            {"breakdown", "Breakdown"},
            {"gtr_break", "GtrBreak"},
            {"bass_break", "BassBreak"},
            {"drum_break", "DrumBreak"},
            {"organ_break", "OrganBreak"},
            {"synth_break", "SynthBreak"},
            {"piano_break", "PianoBreak"},
            {"keyboard_break", "KeyboardBreak"},
            {"horn_break", "HornBreak"},
            {"scratch_break", "ScratchBreak"},
            {"sctrach_break", "ScratchBreak"},
            {"perc_break", "PercussionBreak"},
            {"dj_break", "DJBreak"},
            {"interlude", "Interlude"},
            {"soundscape", "Soundscape"},
            {"jam", "Jam"},
            {"space_jam", "SpaceJam"},
            {"vamp", "Vamp"},
            {"build_up", "Build up"},
            {"speedup", "Speed up"},
            {"tension", "Tension"},
            {"release", "Release"},
            {"crescendo", "Crescendo"},
            {"melody", "Melody"},
            {"lo_melody", "LowMelody"},
            {"hi_melody", "HighMelody"},
            {"main_riff", "MainRiff"},
            {"verse_riff", "VerseRiff"},
            {"chorus_riff", "ChorusRiff"},
            {"gtr_riff", "GtrRiff"},
            {"bass_riff", "BassRiff"},
            {"big_riff", "BigRiff"},
            {"bigger_riff", "BiggerRiff"},
            {"heavy_riff", "HeavyRiff"},
            {"fast_riff", "FastRiff"},
            {"slow_riff", "SlowRiff"},
            {"swing_riff", "SwingRiff"},
            {"chunky_riff", "ChunkyRiff"},
            {"odd_riff", "OddRiff"},
            {"hook", "Hook"},
            {"drum_roll", "DrumRoll"},
            {"gtr_lead", "GtrLead"},
            {"gtr_fill", "GtrFill"},
            {"gtr_hook", "GtrHook"},
            {"gtr_melody", "GtrMelody"},
            {"gtr_line", "GtrLine"},
            {"gtr_lick", "GtrLick"},
            {"vocal_break", "VocalBreak"},
            {"ah", "Ah"},
            {"yeah", "Yeah"},
            {"oohs", "OohsAndAhs"},
            {"prayer", "Prayer"},
            {"chant", "Chant"},
            {"spoken_word", "SpokenWord"},
            {"outro", "Outro"},
            {"outro_solo", "OutroSolo"},
            {"outro_chorus", "OutroChorus"},
            {"ending", "Ending"},
            {"bre", "BigRockEnding"},
            {"fade_out", "FadeOut"}
        };
    }
}