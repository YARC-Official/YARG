using System;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Menu.Persistent
{
    public class ActivePlayerListItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _playerNameText;
        [SerializeField]
        private Image _playerInstrumentIcon;

        public bool ShowName
        {
            get => _playerNameText.gameObject.activeSelf;
            set => _playerNameText.gameObject.SetActive(value);
        }

        public void Initialize(YargPlayer player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var profile = player.Profile;
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(player.Profile));
            }

           _playerNameText.text = profile.Name;

            var vocalistWithNoMic = player.Bindings.Microphone == null && profile.GameMode == Core.GameMode.Vocals && ! profile.IsBot;

            var instrumentSprite = vocalistWithNoMic
                ? "NoMicrophoneIcon"
                : ($"InstrumentIcons[{profile.CurrentInstrument.ToResourceName()}]");
            _playerInstrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>(instrumentSprite)
                .WaitForCompletion();
        }
    }
}
