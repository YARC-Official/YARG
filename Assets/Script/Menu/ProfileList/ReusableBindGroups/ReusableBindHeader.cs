using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Localization;
using YARG.Menu.Data;

namespace YARG.Menu.ProfileList
{
    public class ReusableBindHeader : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _bindingNameText;
        [SerializeField]
        private Image _bindingIcon;

        [Space]
        [SerializeField]
        private Button _settingsButton;
        [SerializeField]
        private GameObject _dropdownArrow;

        [SerializeField]
        private Button _addNewButton;

        public event Action BindingsClicked;
        public event Action SettingsClicked;

        private ReusableBindGroup _group;
        private ReusableControlBinding _binding;

        public void Init(ReusableBindGroup group, ReusableControlBinding binding, bool interactable, bool showLeftyNames, bool isMenu)
        {
            _group = group;
            _binding = binding;
            _group.HandednessChanged += RefreshHandedness;
            RefreshHandedness(showLeftyNames);

            var icons = MenuData.NavigationIcons;
            if (isMenu && icons.HasIcon((MenuAction) binding.Action))
            {
                // Show icons for menu actions
                _bindingIcon.gameObject.SetActive(true);

                _bindingIcon.sprite = icons.GetIcon((MenuAction) binding.Action);
                _bindingIcon.color = icons.GetColor((MenuAction) binding.Action);
            }
            else
            {
                // Don't for anything else
                _bindingIcon.gameObject.SetActive(false);
            }

            _addNewButton.interactable = interactable;
        }

        public void SetArrowOpen(bool open)
        {
            float arrowScale = open ? -1f : 1f;
            _dropdownArrow.transform.localScale =
                _dropdownArrow.transform.localScale.WithY(arrowScale);
        }

        public void SetSettingsButtonActive(bool active)
        {
            var colors = _settingsButton.colors;
            colors.colorMultiplier = active ? 0.75f : 1f;
            _settingsButton.colors = colors;
        }

        public void OnBindingsClicked()
        {
            BindingsClicked?.Invoke();
        }

        public void OnSettingsClicked()
        {
            SettingsClicked?.Invoke();
        }

        public void RefreshHandedness(bool lefty)
        {
            _bindingNameText.text = Localize.Key(lefty ? _binding.NameLefty : _binding.Name);
        }

        public void SetInteractable(bool interactable)
        {
            _addNewButton.interactable = interactable;
        }

        private void OnDestroy()
        {
            _group.HandednessChanged -= RefreshHandedness;
        }
    }
}
