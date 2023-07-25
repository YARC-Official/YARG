using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Helpers.Extensions;

namespace YARG.Menu.InputDeviceDialog
{
    public class InputDeviceDialogMenu : MonoBehaviour
    {
        private static InputDeviceDialogMenu _instance;
        private static InputDevice _selectedDevice;

        [SerializeField]
        private Transform _deviceContainer;

        [Space]
        [SerializeField]
        private GameObject _deviceEntryPrefab;

        private void Awake()
        {
            _instance = this;
        }

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

        private static void SelectDevice(InputDevice inputDevice)
        {
            _selectedDevice = inputDevice;
            MenuNavigator.Instance.PopMenu();
        }

        public static async UniTask<InputDevice> Show()
        {
            // Open dialog
            MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.InputDeviceDialog);

            // Wait until the dialog is closed
            await UniTask.WaitUntil(() => !_instance.gameObject.activeSelf);

            // Return the result
            return _selectedDevice;
        }
    }
}
