using System;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Helpers.Extensions;

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

        public void Initialize(YargProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            _playerNameText.text = profile.Name;
            _playerInstrumentIcon.sprite = Addressables
                .LoadAssetAsync<Sprite>($"InstrumentIcons[{profile.CurrentInstrument.ToResourceName()}]")
                .WaitForCompletion();
        }
    }
}
