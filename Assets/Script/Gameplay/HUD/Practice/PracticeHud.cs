using System;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;

namespace YARG.Gameplay.HUD
{
    public class PracticeHud : GameplayBehaviour
    {
        [Header("References")]
        [SerializeField]
        private TextMeshProUGUI _speedPercentText;

        [SerializeField]
        private TextMeshProUGUI _sectionHeaderText;
        [SerializeField]
        private TextMeshProUGUI _sectionText;
        [SerializeField]

        private TextMeshProUGUI _percentHitText;
        [SerializeField]
        private TextMeshProUGUI _bestPercentText;

        [SerializeField]
        private TextMeshProUGUI _notesHitTotalText;
        [SerializeField]
        private LayoutGroup _guidePitchPartGroup;
        [SerializeField]
        private TextMeshProUGUI _guidePitchPartText;

        // The orange menu-button hint shown under the guide pitch status, so players can see which
        // control toggles it. Rather than duplicate the art, this spawns an instance of the shared
        // HelpBarButton prefab and drives it with an Orange navigation entry, so it looks and behaves
        // exactly like the orange button in the help bar. Wired in the scene.
        [SerializeField]
        private GameObject _guidePitchButtonPrefab;
        [SerializeField]
        private Transform _guidePitchButtonParent;

        /// <summary>Raised when the guide pitch hint button is clicked with the mouse.</summary>
        public event Action GuidePitchToggleRequested;

        private float _speed;
        private float _percentHit;
        private float _bestPercentHit;

        private int _notesHit;

        private Section[] _sections;
        private string[]  _sectionNames;

        private int _currentSectionIndex;

        private bool _speedChanged = false;
        private int  _offsetNotesHit = 0;

        protected override void GameplayAwake()
        {
            _sections = Array.Empty<Section>();
            _currentSectionIndex = 0;
        }

        private void Start()
        {
            if (!GameManager.IsPractice)
            {
                Destroy(gameObject);
            }
        }

        protected override void OnSongStarted()
        {
            // Not OnSongLoaded: SongLoaded is fired from FinalizeChart, which runs before
            // CreatePlayers, so the player list is still empty there and HasVocalsPlayer always
            // reported false. SongStarted is raised after the players are spawned, and is the
            // same hook PracticeManager uses to build the guide pitch manager.
            _guidePitchPartGroup.gameObject.SetActive(HasVocalsPlayer());
            SetupGuidePitchButton();
        }

        private void Update()
        {
            if (GameManager.Players is null)
            {
                return;
            }

            _speedPercentText.SetTextFormat("{0:0}%", GameManager.SongSpeed * 100f);

            int notesHit = 0;
            int totalNotes = 0;
            foreach (var player in GameManager.Players)
            {
                notesHit += player.NotesHit;
                totalNotes += player.TotalNotes;
            }

            if (_speedChanged)
            {
                notesHit -= _offsetNotesHit;
            }

            if (totalNotes == 0)
            {
                _percentHit = 0f;
            }
            else
            {
                _percentHit = (float)notesHit / totalNotes;
            }

            _notesHitTotalText.SetTextFormat("{0}/{1}", notesHit, totalNotes);
            _percentHitText.SetTextFormat("{0}%", Mathf.FloorToInt(_percentHit * 100));

            while(_currentSectionIndex < _sections.Length && GameManager.SongTime >= _sections[_currentSectionIndex].TimeEnd)
            {
                _currentSectionIndex++;

                if(_currentSectionIndex < _sections.Length)
                {
                    _sectionText.text = _sectionNames[_currentSectionIndex];
                }
            }
        }

        public void ResetPractice()
        {
            _speedChanged = false;

            if (_percentHit > _bestPercentHit)
            {
                _bestPercentHit = _percentHit;

                _bestPercentText.SetTextFormat("{0}%", Mathf.FloorToInt(_percentHit * 100));
            }

            _currentSectionIndex = 0;

            if (_sections.Length > 0)
            {
                _sectionText.text = _sectionNames[_currentSectionIndex];
            }
        }

        public void ResetStats()
        {
            if (GameManager.Players != null)
            {
                _offsetNotesHit = 0;
                foreach (var player in GameManager.Players)
                {
                    _offsetNotesHit += player.NotesHit;
                }
            }

            _speedChanged = true;
            _percentHit = 0f;
            _bestPercentHit = 0f;
            _bestPercentText.text = "0%";
        }

        public void SetSections(Section[] sections)
        {
            _sections = sections;
            _sectionNames = new string[sections.Length];
            for (int i = 0; i < sections.Length; i++)
            {
                _sectionNames[i] = PracticeSectionHelper.ParseSectionName(sections[i].Name);
            }
            _currentSectionIndex = 0;

            _percentHit = 0f;
            _bestPercentHit = 0f;
            _offsetNotesHit = 0;
            _speedChanged = false;

            _bestPercentText.text = "0%";
            _sectionText.text = _sectionNames[_currentSectionIndex];
        }

        public void SetGuidePitchPartText(string status, Color color)
        {
            if (_guidePitchPartText != null)
            {
                _guidePitchPartText.text = status;
                _guidePitchPartText.color = color;
            }
        }

        // Spawns the orange menu-button hint from the shared HelpBarButton prefab and points it at an
        // Orange navigation entry with a "Toggle" label. HelpBarButton fills in the sprite, color and
        // "-" glyph from the same navigation icons the help bar uses, and its own click handling
        // invokes the entry, so clicking the hint raises the same toggle the controller's Orange
        // button does.
        private void SetupGuidePitchButton()
        {
            if (_guidePitchButtonPrefab == null || _guidePitchButtonParent == null ||
                MenuData.Instance == null || !HasVocalsPlayer())
            {
                return;
            }

            var buttonObject = Instantiate(_guidePitchButtonPrefab, _guidePitchButtonParent);
            var button = buttonObject.GetComponent<HelpBarButton>();
            button.SetInfoFromSchemeEntry(new NavigationScheme.Entry(
                MenuAction.Orange, "Gameplay.Practice.GuidePitchToggle",
                () => GuidePitchToggleRequested?.Invoke()));

            // The prefab is a fixed width tuned for the help-bar strip, which is too narrow for a
            // short label here, so the text overflows its right margin and runs into the button's
            // edge. A preferred-width fitter sizes the button to its content instead, restoring the
            // label's built-in right padding (and adapting to whatever the localized label is).
            var fitter = buttonObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

            // Center the spawned button in the hint slot.
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
        }

        private bool HasVocalsPlayer()
        {
            if (GameManager.Players is null)
            {
                return false;
            }

            foreach (var player in GameManager.Players)
            {
                var instrument = player.Player.Profile.CurrentInstrument;
                if (instrument is Instrument.Vocals or Instrument.Harmony)
                {
                    return true;
                }
            }

            return false;
        }
    }
}