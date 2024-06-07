using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

namespace YARG.Menu.Dialogs
{
    public class ListWithSettingsDialog : ListDialog
    {
        [Space]
        [SerializeField]
        private Transform _settingsContainer;
        [SerializeField]
        private ListToggleSetting _toggleSettingPrefab;

        public T AddSetting<T>(T prefab)
            where T : Object
        {
            return Instantiate(prefab, _settingsContainer);
        }

        public ListToggleSetting AddToggleSetting(string label, bool initialState, UnityAction<bool> onToggled)
        {
            var toggle = AddSetting(_toggleSettingPrefab);

            toggle.Label = label;
            toggle.Toggled = initialState;
            toggle.OnToggled.AddListener(onToggled);

            // Force canvas to update layout
            // TODO: this doesn't work; why?
            if (transform is RectTransform rect)
            {
                rect.ForceUpdateRectTransforms();
                LayoutRebuilder.MarkLayoutForRebuild(rect);
            }

            return toggle;
        }

        public void ClearSettings()
        {
            _settingsContainer.DestroyChildren();
        }

        public override void ClearDialog()
        {
            base.ClearDialog();

            ClearList();
        }
    }
}
