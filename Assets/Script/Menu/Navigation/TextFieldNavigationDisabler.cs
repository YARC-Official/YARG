using TMPro;
using UnityEngine;

namespace YARG.Menu.Navigation
{
    public class TextFieldNavigationDisabler : MonoBehaviour
    {
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
                Navigator.Instance.PushScheme(NavigationScheme.Empty);
                _navPushed = true;
            }
        }

        private void EnableInputs()
        {
            if (_navPushed)
            {
                Navigator.Instance.PopScheme();
                _navPushed = false;
            }
        }
    }
}