using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Helpers.Extensions;
using YARG.Input;
using YARG.Player;

namespace YARG.Menu.ProfileInfo
{
    // TODO: Clean this up when we get real UI
    public class InputControlDialogMenu : MonoBehaviour
    {
        private ControlBinding _binding;
        private ActuationSettings _bindSettings = new();
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

        [Space]
        [SerializeField]
        private GameObject _controlEntryPrefab;

        public async UniTask<bool> Show(YargPlayer player, ControlBinding binding)
        {
            _binding = binding;
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
            var bindingToken = _cancellationToken.Token;

            try
            {
                var possibleControls = await InputControlBindingHelper.Instance.GetControl(player, bindingToken, _binding);
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
                binding.AddControl(_bindSettings, _grabbedControl);
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