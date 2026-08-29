using UnityEngine;

namespace YARG.Menu.DifficultySelect
{
    /// <summary>
    /// Marks the item as a special button in an arbitrary accent color: accent
    /// text normally and, while selected, a color that fills the entire button
    /// background behind dark text. Same treatment as the ready/sit-out buttons,
    /// but driven by colors instead of a per-color sprite. Lives on the
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

        /// <summary>
        /// Applies the color that fills the button background while selected.
        /// </summary>
        public void SetSelectionFill(Color fillColor)
        {
            _item ??= GetComponent<DifficultyItem>();
            _item.SetSelectionFill(fillColor);
        }
    }
}
