using UnityEngine;

namespace YARG.Gameplay.HUD
{
    public class DraggableHudParent : MonoBehaviour
    {
        public DraggableHudElement SelectedElement { get; private set; }

        public void SetSelectedElement(DraggableHudElement element)
        {
            // Deselect the last element
            if (SelectedElement != null)
            {
                SelectedElement.Deselect();
            }

            // Select the new element
            SelectedElement = element;
            SelectedElement.Select();
        }
    }
}