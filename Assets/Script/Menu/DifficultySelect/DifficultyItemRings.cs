using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.MusicLibrary;

namespace YARG.Menu.DifficultySelect
{
    /// <summary>
    /// Hosts the instrument tier-wheel row on a difficulty item. Lives on the
    /// DifficultyItemRings prefab variant.
    /// </summary>
    [RequireComponent(typeof(DifficultyItem))]
    public class DifficultyItemRings : MonoBehaviour
    {
        /// <summary>
        /// Clones the given ring into a centered horizontal group on a new
        /// layout row below the text, and returns the created rings.
        /// </summary>
        public DifficultyRing[] AttachRingRow(DifficultyRing template, int count,
            float size = 56f, float spacing = 10f)
        {
            // A direct child of the item root joins the VerticalLayoutGroup as
            // a real row; the LayoutElement height makes the item grow to fit.
            var row = new GameObject("RingRow", typeof(RectTransform), typeof(LayoutElement));
            row.layer = gameObject.layer;

            var rowRect = (RectTransform) row.transform;
            rowRect.SetParent(transform, false);
            rowRect.sizeDelta = new Vector2(
                ((RectTransform) transform).sizeDelta.x, size + 8f);

            var layout = row.GetComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = size + 8f;

            var rings = new DifficultyRing[count];
            for (int i = 0; i < count; i++)
            {
                var ring = Instantiate(template, rowRect);
                var rt = (RectTransform) ring.transform;

                // Normalize to the ring prefab's native 65x65 rect and scale
                // uniformly, centered in the row.
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(65f, 65f);
                rt.localScale = Vector3.one * (size / 65f);
                rt.anchoredPosition = new Vector2((i - (count - 1) * 0.5f) * (size + spacing), 0f);

                ring.gameObject.SetActive(true);
                rings[i] = ring;
            }

            return rings;
        }
    }
}
