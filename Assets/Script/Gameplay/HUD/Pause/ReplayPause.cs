using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Gameplay.Player;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Settings.Customization;

namespace YARG.Gameplay.HUD
{
    public class ReplayPause  : GenericPause
    {
        [SerializeField]
        private GameObject _separatorObject;
        [SerializeField]
        private GameObject _saveColorObject;
        private BasePlayer _thisPlayer;
        private ColorProfile _colorProfile;
        private NavigationGroup _navigationGroup;

        // get _thisPlayer and _colorProfile before OnEnable
        protected override void GameplayAwake()
        {
            base.GameplayAwake();

            _navigationGroup = GetComponentInChildren<NavigationGroup>();

            // get player info
            _thisPlayer = GameManager.Players[0];
            _colorProfile = _thisPlayer.Player.ColorProfile;

            // If the user already has the color profile, remove the Save button
            if (CustomContentManager.ColorProfiles.HasPresetId(_colorProfile.Id))
            {
                _separatorObject.SetActive(false);
                _saveColorObject.SetActive(false);
                _navigationGroup.RemoveNavigatable(_saveColorObject.GetComponent<NavigatableBehaviour>());
            }
        }

        public void SaveColorProfile()
        {
            // save the color profile
            CustomContentManager.ColorProfiles.AddPreset(_colorProfile);
            CustomContentManager.ColorProfiles.SaveAll();

            // get Name object then set text to Saved!
            _saveColorObject.GetComponentInChildren<TextMeshProUGUI>().text = Localize.Key("Menu.Common.Saved");

            // remove the onclick listeners to prevent spamming
            _saveColorObject.GetComponentInChildren<NavigatableButton>().RemoveOnClickListeners();
        }
    }
}