using TMPro;
using UnityEngine;
using YARG.Core.Game;
using YARG.Menu.Navigation;
using YARG.Settings.Customization;

namespace YARG.Gameplay.HUD
{
    public class ReplayPause  : GenericPause
    {
        [SerializeField]
        private GameObject saveColorObject;

        public void SaveColorProfile()
        {
            // Get the 1st player
            var thisPlayer = GameManager.Players[0];

            // get the color profile
            var colorProfile = thisPlayer.Player.ColorProfile;

            // if ColorProfile is Default, destroy the saveColorObject and return
            if (colorProfile.Name == ColorProfile.Default.Name)
            {
                Destroy(saveColorObject);
                return;
            }

            // save the color profile
            CustomContentManager.ColorProfiles.SaveItem(colorProfile);

            // refresh the color profile
            CustomContentManager.ColorProfiles.LoadFiles();

            // get Name object then set text to Saved!
            saveColorObject.GetComponentInChildren<TextMeshProUGUI>().text = "Saved!";

            // remove the onclick listeners to prevent spamming
            saveColorObject.GetComponentInChildren<NavigatableButton>().RemoveOnClickListeners();

        }
    }
}