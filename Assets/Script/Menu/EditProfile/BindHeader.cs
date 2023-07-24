using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;

namespace YARG.Menu.EditProfile
{
    public class BindHeader : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;

        private string _bindingName;

        public void Init(string bindingName)
        {
            _bindingName = bindingName;

            _bindingNameText.StringReference = new LocalizedString
            {
                TableReference = "Bindings",
                TableEntryReference = _bindingName
            };
        }

        protected override void OnSelectionChanged(bool selected)
        {
        }
    }
}