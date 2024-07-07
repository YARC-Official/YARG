using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Song;

namespace YARG.Menu.MusicLibrary
{
    // TODO: This should probably be redone, but I'm waiting until we refactor how the icons work
    public class DifficultyRing : MonoBehaviour, IPointerClickHandler
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

        private SongSearchingField _songSearchingField;
        private Instrument _instrument;
        private int _intensity;
        private bool _active;

        private void Awake()
        {
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
                _active = false;
            }
            else
            {
                _active = true;
                if (values.Intensity < 0)
                {
                    values.Intensity = 0;
                }
                else if (values.Intensity > 6)
                {
                    values.Intensity = 6;
                }
            }

            // Set ring sprite
            _ringSprite.sprite = _ringSprites[values.Intensity + 1];

            // Set instrument opacity
            var color = _instrumentIcon.color;
            color.a = values.Intensity > -1 ? 1f : 0.2f;
            _instrumentIcon.color = color;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_active)
            {
                if (eventData.button == PointerEventData.InputButton.Right)
                {
                    _songSearchingField.SetSearchInput(_instrument.ToSortAttribute(), $"\"{_intensity}\"");
                }
                else if (eventData.button == PointerEventData.InputButton.Left)
                {
                    _songSearchingField.SetSearchInput(_instrument.ToSortAttribute(), $"");
                }
            }
        }
    }
}