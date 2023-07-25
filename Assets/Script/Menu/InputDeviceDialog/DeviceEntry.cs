using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YARG.Menu.InputDeviceDialog
{
    public class DeviceEntry : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _deviceName;

        private InputDevice _inputDevice;
        private Action<InputDevice> _selectCallback;

        public void Init(InputDevice inputDevice, Action<InputDevice> selectCallback)
        {
            _inputDevice = inputDevice;
            _selectCallback = selectCallback;

            _deviceName.text = inputDevice.displayName;
        }

        public void SelectDevice()
        {
            _selectCallback(_inputDevice);
        }
    }
}