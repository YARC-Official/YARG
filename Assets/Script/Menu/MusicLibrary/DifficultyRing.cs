using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
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
        [SerializeField]
        private Material _ringRainbowMaterial;
        [SerializeField]
        private Color _partSelectedColor;

        private SongSearchingField _songSearchingField;
        private Instrument _instrument;
        private int _intensity;
        private bool _active;

        private const float ACTIVE_OPACITY = 1f;
        private const float INACTIVE_OPACITY = 0.2f;

        private void Awake()
        {
            _songSearchingField = FindFirstObjectByType<SongSearchingField>();
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
                if (values.Intensity < 1)
                {
                    ringCount = 0;
                }
                else
                {
                    ringCount = 1 + (uint) ((values.Intensity - 1) % 5);
                }
            }

            // Determine ring color and set intensity number text
            var ringColor = _ringWhiteColor;
            var ringBaseColor = _ringEmptyColor;
            Material ringMaterial = null;
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
                        case > 15:
                            ringMaterial = _ringRainbowMaterial;
                            break;
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
            _ringSprite.material = ringMaterial;

            // Set opacity
            if (_active)
            {
                _ringSprite.color = _ringSprite.color.WithAlpha(ACTIVE_OPACITY);
                _ringBase.color = _ringBase.color.WithAlpha(ACTIVE_OPACITY);
            }
            else
            {
                _ringSprite.color = _ringSprite.color.WithAlpha(INACTIVE_OPACITY);
                _ringBase.color = _ringBase.color.WithAlpha(INACTIVE_OPACITY);
            }

            UpdateIconColor();
        }

        private void UpdateIconColor()
        {
            if (!_active)
            {
                _instrumentIcon.color = Color.white.WithAlpha(INACTIVE_OPACITY);
                return;
            }
            if (_songSearchingField.HasInstrumentFilter(_instrument))
            {
                _instrumentIcon.color = _partSelectedColor.WithAlpha(ACTIVE_OPACITY);
                return;
            }
            _instrumentIcon.color = Color.white.WithAlpha(ACTIVE_OPACITY);
        }

        public void OnPointerClick(PointerEventData eventData)
        {

            if (!_active)
            {
                return;
            }

            if (eventData.button == PointerEventData.InputButton.Right)
            {
                _songSearchingField.SetSearchInput(_instrument.ToSortAttribute(), $"\"{_intensity}\"");
            }
            else if (eventData.button == PointerEventData.InputButton.Left)
            {
                if (_instrument == Instrument.Band)
                {
                    // Don't allow filtering by "Band Instrument". That would be silly.
                    return;
                }
                _songSearchingField.SetSearchInput(_instrument.ToSortAttribute(), $"");
            }

            UpdateIconColor();
        }
    }
}