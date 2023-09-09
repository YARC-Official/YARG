using TMPro;
using UnityEngine;
using YARG.Settings.Customization;

namespace YARG.Gameplay.HUD
{
    public class ReplayPause  : GenericPause
    {
        [SerializeField]
        private GameObject saveColorObject;

        protected override void GameplayAwake()
        {
            base.GameplayAwake();

            if (!GameManager.IsReplay)
            {
                Destroy(gameObject);
            }
        }

        public void SaveColorProfile()
        {
            // Get the 1st player
            var thisPlayer = GameManager.Players[0];

            // get the color profile
            var colorProfile = thisPlayer.Player.ColorProfile;

            // save the color profile
            CustomContentManager.ColorProfiles.SaveItem(colorProfile);

            // refresh the color profile
            CustomContentManager.ColorProfiles.LoadFiles();

            // get Name object then set text to Saved!
            saveColorObject.GetComponentInChildren<TextMeshProUGUI>().text = "Saved!";

            // remove the onclick listeners to prevent spamming
            //saveColorObject.GetComponentInChildren<NavigatableButton>().RemoveOnClickListeners();

        }
    }
}