using UnityEngine;
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

        private void RefreshList()
        {
            _deviceContainer.DestroyChildren();
        }
    }
}
