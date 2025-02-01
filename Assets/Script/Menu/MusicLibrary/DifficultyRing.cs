using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Song;
using YARG.Helpers.Extensions;
using YARG.Settings;

namespace YARG.Menu.MusicLibrary
{
    public enum DifficultyRingMode
    {
        Classic,
        Expanded,
    }

    public class DifficultyRing : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField]
        private Image _instrumentIcon;

        [SerializeField]
        private Image _ringSprite;
        [SerializeField]
        private Image _ringBase;

        [SerializeField]
        private TextMeshProUGUI _intensityNumber;

        [Space]
        [SerializeField]
        private Color _ringEmptyColor;
        [SerializeField]
        private Color _ringWhiteColor;
        [SerializeField]
        private Color _ringRedColor;
        [SerializeField]
        private Color _ringPurpleColor;

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

            // Determine how many rings to use
            uint ringCount;
            if (values.SubTracks == 0)
            {
                // No part
                _active = false;
                ringCount = 0;
                // Parts which copy their intensities from other instruments
                // may have a greater-than-zero value here
                values.Intensity = 0;
            }
            else
            {
                // Part present
                _active = true;
                if (values.Intensity < 0)
                {
                    ringCount = 0;
                }
                else
                {
                    ringCount = (uint) (values.Intensity % 5);
                }
            }

            // Determine ring color and set intensity number text
            var ringColor = _ringWhiteColor;
            var ringBaseColor = _ringEmptyColor;
            switch (SettingsManager.Settings.DifficultyRings.Value)
            {
                case DifficultyRingMode.Classic:
                {
                    if (values.Intensity > 5)
                    {
                        ringCount = 5;
                        ringColor = _ringRedColor;
                    }

                    _intensityNumber.text = values.Intensity > 6
                        ? values.Intensity.ToString()
                        : string.Empty;
                    break;
                }
                case DifficultyRingMode.Expanded:
                {
                    if (values.Intensity > 15)
                    {
                        ringCount = 5;
                    }

                    switch (values.Intensity)
                    {
                        // TODO: Rainbow effect
                        // case > 15:
                        //     break;
                        case > 10:
                            ringColor = _ringPurpleColor;
                            ringBaseColor = _ringRedColor;
                            break;
                        case > 5:
                            ringColor = _ringRedColor;
                            ringBaseColor = _ringWhiteColor;
                            break;
                    }

                    _intensityNumber.text = values.Intensity > 5
                        ? values.Intensity.ToString()
                        : string.Empty;
                    break;
                }
            }

            // Set ring sprite properties
            float fill = ringCount / 5f;
            _ringSprite.fillAmount = fill;
            _ringBase.fillAmount = 1 - fill;
            _ringSprite.color = ringColor;
            _ringBase.color = ringBaseColor;

            // Set opacity
            const float ACTIVE_OPACITY = 1f;
            const float INACTIVE_OPACITY = 0.2f;
            if (_active)
            {
                _instrumentIcon.color = _instrumentIcon.color.WithAlpha(ACTIVE_OPACITY);
                _ringSprite.color = _ringSprite.color.WithAlpha(ACTIVE_OPACITY);
                _ringBase.color = _ringBase.color.WithAlpha(ACTIVE_OPACITY);
            }
            else
            {
                _instrumentIcon.color = _instrumentIcon.color.WithAlpha(INACTIVE_OPACITY);
                _ringSprite.color = _ringSprite.color.WithAlpha(INACTIVE_OPACITY);
                _ringBase.color = _ringBase.color.WithAlpha(INACTIVE_OPACITY);
            }
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