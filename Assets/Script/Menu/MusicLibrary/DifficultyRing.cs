using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Song;

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
        private SongSearchingField _songSearchingField;
        private Instrument _instrument;
        private int _intensity;

        private void Awake()
        {
            _searchButton = GetComponent<Button>();
            _songSearchingField = FindObjectOfType<SongSearchingField>();
        }

        public void SetInfo(string assetName, Instrument instrument, PartValues values)
        {
            // Set instrument icon
            var icon = Addressables.LoadAssetAsync<Sprite>($"InstrumentIcons[{assetName}]").WaitForCompletion();
            _instrumentIcon.sprite = icon;
            _instrument = instrument;
            _intensity = values.Intensity;

            if (values.SubTracks == 0)
            {
                values.Intensity = -1;
            }
            else if (values.Intensity < 0)
            {
                values.Intensity = 0;
            }
            else if (values.Intensity > 6)
            {
                values.Intensity = 6;
            }

            // Set ring sprite
            int index = values.Intensity + 1;
            _ringSprite.sprite = _ringSprites[index];

            // Set instrument opacity
            Color color = _instrumentIcon.color;
            color.a = values.Intensity > -1 ? 1f : 0.2f;
            _instrumentIcon.color = color;

            // Set search filter by instrument
            _searchButton.onClick.RemoveAllListeners();
            if (values.SubTracks > 0)
            {
                _searchButton.onClick.AddListener(SearchFilter);
            }
        }

        private void SearchFilter()
        {
            _songSearchingField.SetSearchInput(_instrument.ToSortAttribute(), $"\"{_intensity}\"");
        }

        private void OnDestroy()
        {
            _searchButton.onClick.RemoveAllListeners();
        }
    }
}