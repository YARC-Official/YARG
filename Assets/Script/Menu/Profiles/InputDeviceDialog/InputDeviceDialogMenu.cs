using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;

namespace YARG.Menu.Profiles
{
    public class InputDeviceDialogMenu : MonoBehaviour
    {
        private InputDevice _selectedDevice;

        [SerializeField]
        private Transform _deviceContainer;

        [Space]
        [SerializeField]
        private GameObject _deviceEntryPrefab;

        private void OnEnable()
        {
            _selectedDevice = null;
            RefreshList();
        }

        private void RefreshList()
        {
            _deviceContainer.DestroyChildren();

            foreach (var device in InputSystem.devices)
            {
                var button = Instantiate(_deviceEntryPrefab, _deviceContainer);
                button.GetComponent<DeviceEntry>().Init(device, SelectDevice);
            }
        }

        public void Cancel() => SelectDevice(null);

        private void SelectDevice(InputDevice inputDevice)
        {
            _selectedDevice = inputDevice;
            gameObject.SetActive(false);
        }

        public async UniTask<InputDevice> Show()
        {
            // Open dialog
            gameObject.SetActive(true);

            // Wait until the dialog is closed
            await UniTask.WaitUntil(() => !gameObject.activeSelf);

            // Return the result
            return _selectedDevice;
        }
    }
}
