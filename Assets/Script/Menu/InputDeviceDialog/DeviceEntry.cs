using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YARG.Menu
{
    public class DeviceEntry : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _deviceName;

        private InputDevice _inputDevice;

        public void Init(InputDevice inputDevice)
        {
            _inputDevice = inputDevice;

            _deviceName.text = inputDevice.displayName;
        }
    }
}