using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Assets.Script.Menu.ProfileList;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Input.Bindings;
using YARG.Localization;

namespace YARG.Menu.ProfileInfo
{
    public class DummyControllerRecordDialogMenu : MonoBehaviour
    {
        private InputControl _grabbedControl;

        private readonly List<InputControl> _possibleControls = new();

        private CancellationTokenSource _cancellationToken;
        private CancellationTokenSource _bindingTokenSource;

        [SerializeField]
        private Transform _controlContainer;
        [SerializeField]
        private GameObject _controlChooseContainer;
        [SerializeField]
        private GameObject _waitingContainer;
        [SerializeField]
        private TextMeshProUGUI _waitingText;
        [SerializeField]
        private TextMeshProUGUI _selectText;

        [Space]
        [SerializeField]
        private GameObject _controlEntryPrefab;

        public async UniTask<bool> Show<TSingleState>(InputDevice controller, ReusableSingleBinding<TSingleState> single, BindingType bindingType)
            where TSingleState : struct
        {
            _waitingText.text = Localize.KeyFormat("Menu.ProfileList.Record.Waiting", controller.displayName);
            _selectText.text = Localize.Key("Menu.ProfileList.Record.Select");

            _grabbedControl = null;
            _possibleControls.Clear();

            _cancellationToken = new();
            var token = _cancellationToken.Token;

            // Open dialog
            gameObject.SetActive(true);

            // Reset menu
            _controlContainer.DestroyChildren();
            _waitingContainer.SetActive(true);
            _controlChooseContainer.SetActive(false);

            _bindingTokenSource = new CancellationTokenSource();
            var bindingToken = _bindingTokenSource.Token;

            try
            {
                var possibleControls = await InputControlBindingHelper.Instance.GetControl(controller, bindingToken, bindingType);
                _waitingContainer.SetActive(false);
                _controlChooseContainer.SetActive(true);

                if (possibleControls.Count > 1)
                {
                    _possibleControls.AddRange(possibleControls);

                    // Multiple controls actuated, let the user choose
                    RefreshList();

                    // Wait until the dialog is closed
                    await UniTask.WaitUntil(() => !gameObject.activeSelf, cancellationToken: token);
                }
                else if (possibleControls.Count == 1)
                {
                    _grabbedControl = possibleControls[0];
                }
                else
                {
                    return false;
                }

                // Add the binding
                single.ControlPath = BindingSetHelper.TrimControllerName(_grabbedControl, controller);
                single.DisplayName = _grabbedControl.displayName;
                single.SourceLayout = controller.layout;

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            finally
            {
                gameObject.SetActive(false);
            }
        }

        private void RefreshList()
        {
            _controlContainer.DestroyChildren();

            foreach (var bind in _possibleControls)
            {
                var button = Instantiate(_controlEntryPrefab, _controlContainer);
                button.GetComponent<ControlEntry>().Init(bind, SelectControl);
            }
        }

        public void CancelAndClose()
        {
            _bindingTokenSource?.Cancel();
            _bindingTokenSource?.Dispose();
            _cancellationToken?.Cancel();
            _cancellationToken?.Dispose();
            gameObject.SetActive(false);
        }

        private void SelectControl(InputControl control)
        {
            _grabbedControl = control;
            gameObject.SetActive(false);
        }
    }
}