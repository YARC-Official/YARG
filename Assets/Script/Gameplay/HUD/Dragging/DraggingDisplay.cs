using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class DraggingDisplay : MonoBehaviour
    {
        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }
    }
}