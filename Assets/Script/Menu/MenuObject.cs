using UnityEngine;
using UnityEngine.Assertions;

namespace YARG.Menu
{
    public class MenuObject : MonoBehaviour
    {
        [field: SerializeField]
        public MenuNavigator.Menu Menu { get; private set; }

        private void Start()
        {
            Assert.AreNotEqual(Menu, MenuNavigator.Menu.None);
        }
    }
}