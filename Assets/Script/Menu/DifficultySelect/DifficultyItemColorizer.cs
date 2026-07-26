using UnityEngine;

namespace YARG.Menu.DifficultySelect
{
    /// <summary>
    /// Marks the item as a special button using the Ready-button text
    /// treatment in an arbitrary accent color: accent text normally, dark text
    /// on the row's blue selection highlight while selected. Lives on the
    /// DifficultyItemColored prefab variant.
    /// </summary>
    [RequireComponent(typeof(DifficultyItem))]
    public class DifficultyItemColorizer : MonoBehaviour
    {
        private DifficultyItem _item;

        /// <summary>
        /// Applies the accent text and selected text colors.
        /// </summary>
        public void SetButtonColor(Color accentColor, Color selectedAccentColor)
        {
            _item ??= GetComponent<DifficultyItem>();
            _item.SetAccentColors(accentColor, selectedAccentColor);
        }
    }
}
