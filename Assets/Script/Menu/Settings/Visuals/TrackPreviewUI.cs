using TMPro;
using UnityEngine;
using YARG.Menu.Navigation;

namespace YARG.Menu.Settings.Visuals
{
    public class TrackPreviewUI : MonoBehaviour
    {
        [SerializeField] private GameObject _controls;
        [SerializeField] private DropdownSettingVisual _instrument;
        [SerializeField] private DropdownSettingVisual _options;
        [SerializeField] private TextMeshProUGUI _placeholder;

        private const float TEMPLATE_HEIGHT = 300f;
        private const float CAPTION_FONT_MIN = 14f;
        private const float CAPTION_FONT_MAX = 24f;
        private const float ITEM_FONT_SIZE = 22f;

        private NavigationGroup _navigationGroup;

        public DropdownSettingVisual Instrument => _instrument;
        public DropdownSettingVisual Options => _options;

        private void Awake()
        {
            ConfigureDropdown(_instrument, alignRight: false);
            ConfigureDropdown(_options, alignRight: true);
        }

        private void ConfigureDropdown(DropdownSettingVisual visual, bool alignRight)
        {
            if (visual == null)
            {
                return;
            }

            visual.HideLabel();

            var dropdown = visual.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                return;
            }

            if (dropdown.captionText != null)
            {
                dropdown.captionText.textWrappingMode = TextWrappingModes.NoWrap;
                dropdown.captionText.fontSizeMin = CAPTION_FONT_MIN;
                dropdown.captionText.fontSizeMax = CAPTION_FONT_MAX;
            }

            if (alignRight)
            {
                _placeholder.textWrappingMode = TextWrappingModes.NoWrap;
                _placeholder.fontSizeMin = CAPTION_FONT_MIN;
                _placeholder.fontSizeMax = CAPTION_FONT_MAX;
            }

            var template = dropdown.template;
            if (template != null)
            {
                template.pivot = new Vector2(0.5f, 1f);
                template.anchorMin = new Vector2(0f, 0f);
                template.anchorMax = new Vector2(1f, 0f);
                template.sizeDelta = new Vector2(0f, TEMPLATE_HEIGHT);
                template.anchoredPosition = new Vector2(0f, -4f);
            }

            if (dropdown.itemText != null)
            {
                dropdown.itemText.fontSize = ITEM_FONT_SIZE;
            }
        }

        public void Bind(NavigationGroup navigationGroup, string caption)
        {
            Unbind();
            _navigationGroup = navigationGroup;
            _placeholder.text = caption;
            _controls.SetActive(true);
        }

        public void Unbind()
        {
            if (!_controls.activeSelf)
            {
                return;
            }

            if (_navigationGroup != null)
            {
                _navigationGroup.RemoveNavigatable(_options.GetComponent<RuntimeNavigatable>());
                _navigationGroup.RemoveNavigatable(_instrument.GetComponent<RuntimeNavigatable>());
                _navigationGroup = null;
            }
            _controls.SetActive(false);
        }

        private void OnDisable()
        {
            Unbind();
        }
    }
}
