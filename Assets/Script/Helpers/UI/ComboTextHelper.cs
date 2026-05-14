using Cysharp.Text;
using TMPro;

namespace YARG.Helpers.UI
{
    public static class ComboTextHelper
    {
        /// <summary>
        /// Create a TMP cache of all available multiplier texts.
        /// </summary>
        /// <param name="maxMultiplier">The max multiplier to generate WITHOUT Star Power.</param>
        /// <param name="ComboTextPrefab">The prefab of the text to instantiate.</param>
        /// <param name="isMultiplayer"> Whether we are playing multiplayer (and therefore do not need to generate SP multipliers).</param>
        /// <typeparam name="T">TextMeshPro type being used</typeparam>
        /// <returns>Array where the corresponding TMP object is at arr[multiplier - 2]</returns>
        public static T[] CreateComboTextCache<T>(int combo, T ComboTextPrefab) where T : TMP_Text
        {
			
			var textCache = new T[combo - 1];
            for (int i = 2; i <= 99999; i++)
            {
                if (textCache[i - 2] == null)
                {
                    textCache[i - 2] = GenerateComboText(i, ComboTextPrefab);
                }

            }

            return textCache;
        }

        private static T GenerateComboText<T>(int combo, T ComboTextPrefab) where T : TMP_Text
        {
            var text = UnityEngine.Object.Instantiate(ComboTextPrefab, ComboTextPrefab.transform.parent);
            text.SetTextFormat("{0} <sub>COMBO</sub>", combo);
            text.enabled = false;
            return text;
        }
    }
}