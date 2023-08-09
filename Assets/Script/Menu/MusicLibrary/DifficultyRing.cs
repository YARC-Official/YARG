using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Song;
using YARG.Data;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    public class DifficultyRing : MonoBehaviour
    {
        [SerializeField]
        private Image instrumentIcon;

        [SerializeField]
        private Image ringSprite;

        [SerializeField]
        private Sprite[] ringSprites;

        private Button _searchButton;

        private void Awake()
        {
            _searchButton = GetComponent<Button>();
        }

        public void SetInfo(string assetName, string filter, PartValues values)
        {
            // Set instrument icon
            var icon = Addressables.LoadAssetAsync<Sprite>($"FontSprites[{assetName}]").WaitForCompletion();
            instrumentIcon.sprite = icon;

            if (values.subTracks == 0)
            {
                values.intensity = -1;
            }
            else if (values.intensity < 0)
            {
                values.intensity = 0;
            }
            else if (values.intensity > 6)
            {
                values.intensity = 6;
            }

            // Set ring sprite
            int index = values.intensity + 1;
            ringSprite.sprite = ringSprites[index];

            // Set instrument opacity
            Color color = instrumentIcon.color;
            color.a = values.intensity > -1 ? 1f : 0.2f;
            instrumentIcon.color = color;

            // Set search filter by instrument
            _searchButton.onClick.RemoveAllListeners();
            if (values.subTracks > 0)
            {
                _searchButton.onClick.AddListener(() => SearchFilter(filter));
            }
        }

        private void SearchFilter(string instrument)
        {
            MusicLibraryMenu.Instance.SetSearchInput($"instrument:{instrument}");
        }

        private void OnDestroy()
        {
            _searchButton.onClick.RemoveAllListeners();
        }
    }
}