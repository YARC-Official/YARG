using UnityEditor.Localization.Plugins.XLIFF.V12;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using YARG.Input;

namespace YARG.Menu.EditProfile
{
    public class BindHeader : NavigatableBehaviour
    {
        [Space]
        [SerializeField]
        private LocalizeStringEvent _bindingNameText;

        private ControlBinding _binding;

        public void Init(ControlBinding binding)
        {
            _binding = binding;

            _bindingNameText.StringReference = new LocalizedString
            {
                TableReference = "Bindings",
                TableEntryReference = _binding.Name
            };
        }

        protected override void OnSelectionChanged(bool selected)
        {
        }
    }
}