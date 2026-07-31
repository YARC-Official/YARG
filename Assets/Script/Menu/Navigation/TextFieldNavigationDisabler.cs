using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using YARG.Core.Input;

namespace YARG.Menu.Navigation
{
    public class TextFieldNavigationDisabler : MonoBehaviour
    {
        public enum TextFieldTypeEnum
        {
            MusicLibrarySearch,
            Other
        }

        public TextFieldTypeEnum TextFieldType = TextFieldTypeEnum.Other;

        private static readonly NavigationScheme TextFieldScheme = new(new List<NavigationScheme.Entry>
        {
            new(
                MenuAction.Red,
                "Menu.MusicLibrary.ExitSearchHold",
                handler: null,
                onHoldHandler: () => EventSystem.current.SetSelectedGameObject(null),
                holdSeconds: 0.5f,
                hide: false
            ),
            new(
                MenuAction.Search,
                "Menu.MusicLibrary.Search",
                () => EventSystem.current.SetSelectedGameObject(null),
                hide: true
            )
        }, allowsMusicPlayer: null);

        [SerializeField]
        private TMP_InputField _textField;

        private bool _textFocused;
        private bool _navPushed;

        private void OnDisable()
        {
            EnableInputs();
        }

        private void Update()
        {
            // We can't use the "OnSelect" event because for some reason it isn't called
            // if the user reselected the input field after pressing enter.

            if (_textFocused == _textField.isFocused)
                return;

            _textFocused = _textField.isFocused;

            if (_textFocused)
            {
                DisableInputs();
            }
            else
            {
                EnableInputs();
            }
        }

        private void DisableInputs()
        {
            if (!_navPushed)
            {
                if (TextFieldType == TextFieldTypeEnum.MusicLibrarySearch)
                {
                    _ = Navigator.Instance.PushScheme(TextFieldScheme);
                }
                else
                {
                    _ = Navigator.Instance.PushScheme(NavigationScheme.Empty);
                }
                _navPushed = true;
            }
        }

        private void EnableInputs()
        {
            if (_navPushed)
            {
                // Unity moment. Without this the text field cannot be selected again without clicking
                EventSystem.current.SetSelectedGameObject(null);

                Navigator.Instance.PopScheme();
                _navPushed = false;
            }
        }
    }
}