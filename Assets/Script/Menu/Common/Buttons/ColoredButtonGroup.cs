using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using YARG.Menu;
using YARG.Menu.Data;

namespace YARG
{
    public class ColoredButtonGroup : MonoBehaviour
    {
        [SerializeField]
        private List<ColoredButton> _buttons;

        public delegate void OnButtonClicked();
        public OnButtonClicked ClickedButton;

        public ColoredButton ActiveButton { get; private set; }

        private ColoredButton _prevActivatedButton;

        public void OnEnable()
        {
            foreach (var button in _buttons)
            {
                button.OnClick.RemoveAllListeners();
                button.OnClick.AddListener(() => OnClick(button));
            }
        }

        public void ActivateButton(string buttonName)
        {
            foreach (var button in _buttons)
            {
                if (!string.Equals(button.Text.text, buttonName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (ActiveButton != null)
                {
                    ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.DeactivatedButton,
                        MenuData.Colors.BrightText, MenuData.Colors.DeactivatedText);
                }

                ActiveButton = button;
                ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.BrightButton);
                break;
            }
        }

        private void OnClick(ColoredButton button)
        {
            if (_prevActivatedButton == null)
            {
                _prevActivatedButton = button;
            }
            else
            {
                if (ActiveButton != null)
                {
                    _prevActivatedButton = ActiveButton;
                    _prevActivatedButton.SetBackgroundAndTextColor(MenuData.Colors.DeactivatedButton, MenuData.Colors.BrightText, MenuData.Colors.DeactivatedText);
                }
            }

            if (ActiveButton == null || ActiveButton != button)
            {
                ActiveButton = button;
                ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.BrightButton);
            }
            else
            {
                ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.DeactivatedButton, MenuData.Colors.BrightText, MenuData.Colors.DeactivatedText);
                ActiveButton = null;
            }

            ClickedButton?.Invoke();
        }
    }
}
