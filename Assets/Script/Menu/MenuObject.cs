using UnityEngine;
using UnityEngine.Assertions;

namespace YARG.Menu
{
    public class MenuObject : MonoBehaviour
    {
        [field: SerializeField]
        public MenuNavigator.Menu Menu { get; private set; }

        [field: SerializeField]
        public bool HideBelow { get; private set; } = true;

        private void Start()
        {
            Assert.AreNotEqual(Menu, MenuNavigator.Menu.None);
        }

        public void CloseMenu()
        {
            MenuNavigator.Instance.PopMenu();
        }
    }
}