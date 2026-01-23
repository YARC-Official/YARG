using System;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Helpers.Extensions
{
    /// <summary>
    /// Extensions for <see cref="ScrollRect"/> to handle scrolling in units (typically pixels) rather than normalized values.
    /// </summary>
    public static class ScrollViewExtensions
    {
        public static float ScrollableWidth(this ScrollRect scrollRect)
        {
            return Math.Max(0f, scrollRect.content.rect.width - scrollRect.viewport.rect.width);
        }

        public static float ScrollableHeight(this ScrollRect scrollRect)
        {
            return Math.Max(0f, scrollRect.content.rect.height - scrollRect.viewport.rect.height);
        }

        public static float HorizonalPositionInUnits(this ScrollRect scrollRect)
        {
            if (scrollRect.ScrollableWidth() == 0)
            {
                return 0f; // No scrolling needed
            }

            return scrollRect.horizontalNormalizedPosition * scrollRect.ScrollableWidth();
        }

        public static float VerticalPositionInUnits(this ScrollRect scrollRect)
        {
            if (scrollRect.ScrollableHeight() == 0)
            {
                return 0f; // No scrolling needed
            }
            return scrollRect.verticalNormalizedPosition * scrollRect.ScrollableHeight();
        }

        public static void MoveHorizontalInUnits(this ScrollRect scrollRect, float delta)
        {
            if (scrollRect.ScrollableWidth() == 0)
            {
                return; // No scrolling needed
            }

            var position = scrollRect.HorizonalPositionInUnits() + delta;
            scrollRect.horizontalNormalizedPosition = Mathf.Clamp(position / scrollRect.ScrollableWidth(), 0, 1);
        }

        public static void MoveVerticalInUnits(this ScrollRect scrollRect, float delta)
        {
            if (scrollRect.ScrollableHeight() == 0)
            {
                return; // No scrolling needed
            }

            var position = scrollRect.VerticalPositionInUnits() + delta;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp(position / scrollRect.ScrollableHeight(), 0, 1);
        }
    }
}
