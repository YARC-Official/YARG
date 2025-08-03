using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Helpers.UI
{
    /// <summary>
    /// Only allows element to grow, never shrink.
    /// 
    /// </summary>
    [RequireComponent(typeof(RectTransform), typeof(TextMeshProUGUI))]
    [ExecuteInEditMode]
    public class LayoutElementHighWaterMark : MonoBehaviour
    {
        private new RectTransform transform;
        private LayoutElement parentElement;
        private TextMeshProUGUI layoutElement;

        private void OnEnable()
        {
            transform = GetComponent<RectTransform>();
            parentElement = transform.parent.GetComponent<LayoutElement>();
            layoutElement = GetComponent<TextMeshProUGUI>();
        }

        private void Update()
        {
            if (layoutElement.preferredWidth > parentElement.minWidth)
            {
                parentElement.minWidth = layoutElement.preferredWidth;
            }

            if (layoutElement.preferredHeight > parentElement.minHeight)
            {
                parentElement.minHeight = layoutElement.preferredHeight;
            }

            var rect = transform.rect;
            var parentSize = (transform.parent as RectTransform).rect.size;

            transform.rect.Set(rect.x, rect.y, parentSize.x, parentSize.y);
        }
    }
}
