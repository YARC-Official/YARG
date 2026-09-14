using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Helpers.UI
{
    /// <summary>
    ///     Insets a row's or panel's content from the notch side while its
    ///     background keeps bleeding to the screen edge. Classifies the
    ///     direct children of the given transform:
    ///     - stretched children are content containers or text, so their
    ///       left edge moves in and their right edge stays — unless they hug
    ///       both edges with nothing but images beneath them, which makes
    ///       them backgrounds, borders or dividers;
    ///     - left-anchored children with an authored size (icons, labels)
    ///       are shifted;
    ///     - zero-size point holders are left alone.
    ///     No-op off mobile.
    /// </summary>
    public static class SafeAreaContent
    {
        // Backgrounds may overhang their row by a pixel or two to hide seams
        private const float EDGE_TOLERANCE = 2f;

        public static void InsetLeftContent(Transform parent)
        {
            if (!Application.isMobilePlatform || parent == null)
            {
                return;
            }

            foreach (Transform child in parent)
            {
                if (child is not RectTransform rect || rect.GetComponent<SafeAreaBehaviour>() != null)
                {
                    continue;
                }

                bool stretchesX = !Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x);
                if (stretchesX)
                {
                    // A self-sized child takes its width from its content and
                    // hangs from its pivot: shift it when it hangs from the
                    // left, leave it alone when it hangs from the right.
                    // Changing its edges would fight the fitter every frame.
                    if (IsSelfSized(rect))
                    {
                        if (rect.pivot.x < 0.5f)
                        {
                            rect.gameObject.AddComponent<SafeAreaOffset>().Configure(left: true);
                        }

                        continue;
                    }

                    bool hugsEdges = Mathf.Abs(rect.offsetMin.x) <= EDGE_TOLERANCE &&
                        Mathf.Abs(rect.offsetMax.x) <= EDGE_TOLERANCE;
                    if (hugsEdges && !HasContent(rect))
                    {
                        continue;
                    }

                    rect.gameObject.AddComponent<SafeAreaContainer>().Configure(left: true);
                }
                else if (Mathf.Approximately(rect.anchorMin.x, 0f) && rect.sizeDelta != Vector2.zero)
                {
                    rect.gameObject.AddComponent<SafeAreaOffset>().Configure(left: true);
                }
            }
        }

        private static bool IsSelfSized(Component rect)
        {
            return rect.TryGetComponent<ContentSizeFitter>(out var fitter) &&
                fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained;
        }

        private static bool HasContent(Component root)
        {
            return root.GetComponentInChildren<TMP_Text>(true) != null ||
                root.GetComponentInChildren<Selectable>(true) != null;
        }
    }
}
