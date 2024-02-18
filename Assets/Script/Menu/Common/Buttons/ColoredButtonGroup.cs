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

        [SerializeField]
        private bool _trackPreviousButtons;

        public delegate void OnButtonClicked();
        public OnButtonClicked ClickedButton;

        public ColoredButton ActiveButton { get; private set; }

        private readonly List<ColoredButton> _prevActivatedButtons = new();

        public void OnEnable()
        {
            foreach (var button in _buttons)
            {
                button.OnClick.AddListener(() => OnClick(button));
                button.PointerEnter += OnPointerEnter;
                button.PointerExit += OnPointerExit;
            }
        }

        private void OnDisable()
        {
            foreach (var button in _buttons)
            {
                button.OnClick.RemoveAllListeners();
                button.PointerEnter -= OnPointerEnter;
                button.PointerExit -= OnPointerExit;
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
                    ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.DarkButton);

                    if (_trackPreviousButtons)
                    {
                        _prevActivatedButtons.Add(ActiveButton);
                    }
                }

                ActiveButton = button;
                ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.BrightButton);

                if (_trackPreviousButtons && _prevActivatedButtons.Count == 0)
                {
                    _prevActivatedButtons.Add(ActiveButton);
                }

                break;
            }
        }

        public void DeactivateAllButtons()
        {
            if (_trackPreviousButtons)
            {
                _prevActivatedButtons.Clear();
            }

            ActiveButton = null;
            foreach (var button in _buttons)
            {
                button.SetBackgroundAndTextColor(MenuData.Colors.DeactivatedButton,
                    MenuData.Colors.BrightText, MenuData.Colors.DeactivatedText);
            }
        }

        private void OnClick(ColoredButton button)
        {
            if (_prevActivatedButtons.Count == 0)
            {
                _prevActivatedButtons.Add(button);
            }
            else
            {
                if (ActiveButton != null)
                {
                    if (_trackPreviousButtons)
                    {
                        ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.DarkButton);

                        if (!_prevActivatedButtons.Contains(ActiveButton))
                        {
                            _prevActivatedButtons.Add(ActiveButton);
                        }
                    }
                    else
                    {
                        ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.DeactivatedButton, MenuData.Colors.BrightText, MenuData.Colors.DeactivatedText);
                    }
                }
            }

            if (ActiveButton == null || ActiveButton != button)
            {
                ActiveButton = button;
                ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.BrightButton);
            }
            else
            {
                _prevActivatedButtons.Remove(ActiveButton);

                ActiveButton.SetBackgroundAndTextColor(MenuData.Colors.DeactivatedButton, MenuData.Colors.BrightText, MenuData.Colors.DeactivatedText);
                ActiveButton = null;
            }

            ClickedButton?.Invoke();
        }

        private void OnPointerEnter(ColoredButton button)
        {
            if (ActiveButton == button)
            {
                button.SetBackgroundAndTextColor(MenuData.Colors.CancelButton);
            }
            else
            {
                button.SetBackgroundAndTextColor(MenuData.Colors.ConfirmButton);
            }
        }

        private void OnPointerExit(ColoredButton button)
        {
            if (ActiveButton == button)
            {
                button.SetBackgroundAndTextColor(MenuData.Colors.BrightButton);
            }
            else if (_prevActivatedButtons.Contains(button))
            {
                button.SetBackgroundAndTextColor(MenuData.Colors.DarkButton);
            }
            else
            {
                button.SetBackgroundAndTextColor(MenuData.Colors.DeactivatedButton, MenuData.Colors.BrightText, MenuData.Colors.DeactivatedText);
            }
        }
    }
}
