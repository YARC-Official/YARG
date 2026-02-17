using UnityEngine.EventSystems;

namespace YARG.Helpers.Extensions
{
    internal static class PointerEventDataExtensions
    {
        public static bool IsLeftButton(this PointerEventData eventData)
        {
            return eventData.button == PointerEventData.InputButton.Left;
        }
    }
}
