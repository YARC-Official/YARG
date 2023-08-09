using UnityEngine;
using UnityEngine.Assertions;

namespace YARG.Gameplay.HUD
{
    public class PauseMenuObject : MonoBehaviour
    {
        [field: SerializeField]
        public PauseMenuManager.Menu Menu { get; private set; }

        private void Start()
        {
            Assert.AreNotEqual(Menu, PauseMenuManager.Menu.None);
        }
    }
}