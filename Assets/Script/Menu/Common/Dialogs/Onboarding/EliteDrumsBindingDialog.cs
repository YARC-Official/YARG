using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;

namespace YARG.Menu.Dialogs
{
    public class EliteDrumsBindingDialog : FriendlyBindingDialog
    {
        [Header("Pro Drums")]
        [SerializeField]
        private Image _proDrumsKit;
        [SerializeField]
        private Image[] _proDrumsHighlights;

        [Header("Five Lane Drums")]
        [SerializeField]
        private Image _fiveLaneKit;
        [SerializeField]
        private Image[] _fiveLaneHighlights;

        private CurrentMode _currentMode;

        protected override (string initial, string complete) BindingMessages { get; set; } = (
            "You must bind controls for both four lane and five lane mode.\nOnce you have completed this screen, the five lane binding dialog will be shown.",
            "Binding complete.\n\nYou will still need to manually set menu navigation bindings if you have not already."
        );

        public override void Initialize()
        {
            Image = _proDrumsKit;
            _keyHighlights = _proDrumsHighlights;

            base.Initialize();

            // We bind four lane first (at least until Elite Drums actually exists)
            _currentMode = CurrentMode.FourLaneDrums;

            Title.text = "MIDI Drums (Four Lane) Binding";
        }

        private void SetFiveLaneDrums()
        {
            _currentMode = CurrentMode.FiveLaneDrums;
            _mode = GameMode.FiveLaneDrums;

            Title.text = "MIDI Drums (Five Lane) Binding";

            foreach (var highlight in _keyHighlights)
            {
                highlight.gameObject.SetActive(false);
            }

            _keyHighlights = _fiveLaneHighlights;
            foreach (var highlight in _keyHighlights)
            {
                highlight.gameObject.SetActive(false);
            }

            _proDrumsKit.gameObject.SetActive(false);
            _fiveLaneKit.gameObject.SetActive(true);

            Image = _fiveLaneKit;
        }

        protected override void CheckForModeSwitch(string key, GameMode mode)
        {
            if (_currentMode == CurrentMode.FourLaneDrums && key is not "Drums.Kick" && !key.StartsWith("EliteDrums.FourLane"))
            {
                SetFiveLaneDrums();
            }
        }

        protected override bool IsKeyValid(GameMode mode, string key)
        {
            if (mode == GameMode.FourLaneDrums && key.StartsWith("EliteDrums.FiveLane"))
            {
                return false;
            }

            if (mode == GameMode.FiveLaneDrums && (key.StartsWith("EliteDrums.FourLane") ||
                key == "Drums.Kick"))
            {
                return false;
            }

            return true;
        }

        protected override Image GetHighlightByName(GameMode mode, string bindingName)
        {
            // This is confusing since we do both 4 and 5 lane with different prefabs
            var key = bindingName switch
            {
                "EliteDrums.FourLaneRedDrum"      => _keyHighlights[0],
                "EliteDrums.FourLaneYellowDrum"   => _keyHighlights[1],
                "EliteDrums.FourLaneBlueDrum"     => _keyHighlights[2],
                "EliteDrums.FourLaneGreenDrum"    => _keyHighlights[3],
                "EliteDrums.FourLaneYellowCymbal" => _keyHighlights[4],
                "EliteDrums.FourLaneBlueCymbal"   => _keyHighlights[5],
                "EliteDrums.FourLaneGreenCymbal"  => _keyHighlights[6],

                "Drums.Kick" => _currentMode == CurrentMode.FourLaneDrums ? _keyHighlights[7] : _keyHighlights[5],

                "EliteDrums.FiveLaneRedDrum"      => _keyHighlights[0],
                "EliteDrums.FiveLaneBlueDrum"     => _keyHighlights[2],
                "EliteDrums.FiveLaneGreenDrum"    => _keyHighlights[4],
                "EliteDrums.FiveLaneYellowCymbal" => _keyHighlights[1],
                "EliteDrums.FiveLaneOrangeCymbal" => _keyHighlights[3],
                _                                 => null
            };

            return key;
        }

        private enum CurrentMode
        {
            FourLaneDrums,
            FiveLaneDrums
        }
    }
}