using System.Globalization;
using UnityEngine;
using YARG.Input;
using YARG.PlayMode;

namespace YARG.UI
{
    public class EditPlayers : MonoBehaviour
    {
        [SerializeField]
        private GameObject playerSectionPrefab;

        [SerializeField]
        private Transform playerContiner;

        [SerializeField]
        private Transform addButton;

        [SerializeField]
        private TMPro.TMP_InputField micSpeedInput;

        public void SetVocalTrackSpeed()
        {
            MicPlayer.trackSpeed = float.Parse(micSpeedInput.text, CultureInfo.InvariantCulture);
        }

        public void SetVocalSpeedInputText()
        {
            micSpeedInput.text = MicPlayer.trackSpeed.ToString("N2", CultureInfo.InvariantCulture);
        }

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Back, "Back", () => { MainMenu.Instance.ShowMainMenu(); })
            }, true));

            UpdatePlayers();
            SetVocalSpeedInputText();
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        private void UpdatePlayers()
        {
            // Delete old (except add button)
            foreach (Transform t in playerContiner)
            {
                if (t == addButton)
                {
                    continue;
                }

                Destroy(t.gameObject);
            }

            // Create new player sections
            foreach (var player in PlayerManager.players)
            {
                var go = Instantiate(playerSectionPrefab, playerContiner);
                go.GetComponent<PlayerSection>().SetPlayer(player);
            }

            // Put at end
            addButton.SetAsLastSibling();
        }
    }
}