using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core.Song;

namespace YARG.Menu.MusicLibrary
{
    // TODO: This should probably be redone, but I'm waiting until we refactor how the icons work
    public class DifficultyRing : MonoBehaviour
    {
        [FormerlySerializedAs("instrumentIcon")]
        [SerializeField]
        private Image _instrumentIcon;

        [FormerlySerializedAs("ringSprite")]
        [SerializeField]
        private Image _ringSprite;

        [FormerlySerializedAs("ringSprites")]
        [SerializeField]
        private Sprite[] _ringSprites;

        private Button _searchButton;
        private MusicLibraryMenu _musicLibraryMenu;

        private void Awake()
        {
            _searchButton = GetComponent<Button>();
            _musicLibraryMenu = GetComponentInParent<MusicLibraryMenu>();
        }

        public void SetInfo(string assetName, string filter, PartValues values)
        {
            // Set instrument icon
            var icon = Addressables.LoadAssetAsync<Sprite>($"InstrumentIcons[{assetName}]").WaitForCompletion();
            _instrumentIcon.sprite = icon;

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
            _ringSprite.sprite = _ringSprites[index];

            // Set instrument opacity
            Color color = _instrumentIcon.color;
            color.a = values.intensity > -1 ? 1f : 0.2f;
            _instrumentIcon.color = color;

            // Set search filter by instrument
            _searchButton.onClick.RemoveAllListeners();
            if (values.subTracks > 0)
            {
                _searchButton.onClick.AddListener(() => SearchFilter(filter));
            }
        }

        private void SearchFilter(string instrument)
        {
            _musicLibraryMenu.SetSearchInput($"instrument:{instrument}");
        }

        private void OnDestroy()
        {
            _searchButton.onClick.RemoveAllListeners();
        }
    }
}