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

        private NavigationGroup _navigationGroup;

        public DropdownSettingVisual Instrument => _instrument;
        public DropdownSettingVisual Options => _options;

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
