using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Input;

namespace YARG.UI
{
    public class PlayerSection : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI playerName;

        [SerializeField]
        private Image instrumentIcon;

        [SerializeField]
        private TMP_InputField trackSpeedField;

        [SerializeField]
        private Toggle leftyFlipToggle;

        private PlayerManager.Player player;

        public void SetPlayer(PlayerManager.Player player)
        {
            this.player = player;

            playerName.text = player.DisplayName;
            SetInstrumentIcon(player);
            trackSpeedField.text = player.trackSpeed.ToString("N2", CultureInfo.InvariantCulture);
            leftyFlipToggle.isOn = player.leftyFlip;

            // Pro-guitar lefty flip is a little bit more complicated (TODO)
            // Mic doesn't have a lefty flip
            leftyFlipToggle.interactable =
                player.inputStrategy is not RealGuitarInputStrategy &&
                player.inputStrategy is not MicInputStrategy;

            // Mic doesn't have a track speed
            trackSpeedField.interactable =
                player.inputStrategy is not MicInputStrategy;
        }

        private void SetInstrumentIcon(PlayerManager.Player player)
        {
            var iconName = player.inputStrategy.GetIconName();
            var icon = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{iconName}]").WaitForCompletion();
            instrumentIcon.sprite = icon;
        }

        public void DeletePlayer()
        {
            PlayerManager.players.Remove(player);

            player.inputStrategy?.Dispose();

            player = null;
            Destroy(gameObject);
        }

        public void UpdateTrackSpeed()
        {
            if (player != null)
            {
                player.trackSpeed = float.Parse(trackSpeedField.text, CultureInfo.InvariantCulture);
            }
        }

        public void UpdateLeftyFlip()
        {
            if (player != null)
            {
                player.leftyFlip = leftyFlipToggle.isOn;
            }
        }
    }
}