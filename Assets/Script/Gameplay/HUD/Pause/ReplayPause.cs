using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Gameplay.Player;
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

        // get _thisPlayer and _colorProfile before OnEnable
        protected override void GameplayAwake()
        {
            base.GameplayAwake();

            // get player info
            _thisPlayer = GameManager.Players[0];
            _colorProfile = _thisPlayer.Player.ColorProfile;

            // if ColorProfile is Default, remove the Save button
            // if (_colorProfile.Name == ColorProfile.Default.Name)
            // {
            //     _separatorObject.SetActive(false);
            //     _saveColorObject.SetActive(false);
            // }
        }

        public void SaveColorProfile()
        {
            // // save the color profile
            // CustomContentManager.ColorProfiles.SaveItem(_colorProfile);
            //
            // // refresh the color profile
            // CustomContentManager.ColorProfiles.LoadFiles();

            // get Name object then set text to Saved!
            _saveColorObject.GetComponentInChildren<TextMeshProUGUI>().text = "Saved!";

            // remove the onclick listeners to prevent spamming
            _saveColorObject.GetComponentInChildren<NavigatableButton>().RemoveOnClickListeners();
        }
    }
}