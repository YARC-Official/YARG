using UnityEngine;
using UnityEngine.Assertions;
using YARG.Menu.Navigation;

namespace YARG.Menu
{
    public class MenuObject : MonoBehaviour
    {
        [field: SerializeField]
        public MenuManager.Menu Menu { get; private set; }

        [field: SerializeField]
        public bool HideBelow { get; private set; } = true;

        private void Start()
        {
            Assert.AreNotEqual(Menu, MenuManager.Menu.None);
        }

        public void CloseMenu()
        {
            MenuManager.Instance.PopMenu();
        }
    }
}