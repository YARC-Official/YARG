using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Helpers.Extensions;

namespace YARG.Menu
{
    public class InputDeviceDialog : MonoBehaviour
    {
        [SerializeField]
        private Transform _deviceContainer;

        [Space]
        [SerializeField]
        private GameObject _deviceEntryPrefab;

        private void OnEnable()
        {
            RefreshList();
        }

        private void RefreshList()
        {
            _deviceContainer.DestroyChildren();

            foreach (var device in InputSystem.devices)
            {
                var button = Instantiate(_deviceEntryPrefab, _deviceContainer);
                button.GetComponent<DeviceEntry>().Init(device);
            }
        }
    }
}
