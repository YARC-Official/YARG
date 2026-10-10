using System.Collections.Generic;
using System.Threading;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace YARG.Menu.ListMenu
{
    public class ViewObject<TViewType> : MonoBehaviour
        where TViewType : BaseViewType
    {
        private const float MARQUEE_SPEED = 70f;
        private const float MARQUEE_END_PAUSE = 1f;
        private const float MARQUEE_FADE_DURATION = 0.25f;
        private const float MARQUEE_RIGHT_INSET = 15f;

        protected virtual bool AllowMarqueeScroll => false;
        protected virtual Type[] AllowedMarqueeScrollTypes => Array.Empty<Type>();

        [SerializeField]
        private CanvasGroup _canvasGroup;

        [Space]
        [SerializeField]
        protected GameObject NormalBackground;
        [SerializeField]
        protected GameObject SelectedBackground;
        [SerializeField]
        protected GameObject CategoryBackground;

        [Space]
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private List<TextMeshProUGUI> _primaryText;
        [SerializeField]
        private List<TextMeshProUGUI> _secondaryText;

        private bool _marqueeActive;
        private float _marqueeStartTime;

        protected bool Showing { get; private set; }

        protected TViewType ViewType;

        public virtual void Show(bool selected, TViewType viewType)
        {
            Showing = true;
            ViewType = viewType;

            // Set background
            _canvasGroup.alpha = 1f;
            SetBackground(selected, viewType.Background);

            // Set text
            foreach(var i in _primaryText)
            {
                i.text = viewType.GetPrimaryText(selected);
            }
            foreach(var i in _secondaryText)
            {
                i.text = viewType.GetSecondaryText(selected);
            }

            SetIcon(viewType.GetIcon());
            if (AllowMarqueeScroll)
                SetMarqueeActive(selected && IsMarqueeScrollTypeAllowed(viewType));
        }

        public virtual void Hide()
        {
            if (_marqueeActive)
                SetMarqueeActive(false);

            Showing = false;
            _canvasGroup.alpha = 0f;
        }

        private void LateUpdate()
        {
            if (!_marqueeActive) return;
            
            float elapsed = Time.unscaledTime - _marqueeStartTime;
            UpdateMarquee(_primaryText[0], elapsed);
            UpdateMarquee(_secondaryText[0], elapsed);
        }

        private bool IsMarqueeScrollTypeAllowed(TViewType viewType)
        {
            Type viewTypeType = viewType.GetType();
            foreach (Type allowedType in AllowedMarqueeScrollTypes)
            {
                if (viewTypeType == allowedType)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetMarqueeActive(bool active)
        {
            _marqueeActive = active;
            _marqueeStartTime = Time.unscaledTime;

            SetTextOverflow(_primaryText[0], active);
            SetTextOverflow(_secondaryText[0], active);
        }

        private static void SetTextOverflow(TextMeshProUGUI text, bool marquee)
        {
            text.overflowMode = marquee ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            text.ForceMeshUpdate();
        }

        private static void UpdateMarquee(TextMeshProUGUI text, float elapsed)
        {
            text.ForceMeshUpdate();

            float availableWidth = text.rectTransform.rect.width;
            if (text.preferredWidth <= availableWidth)
            {
                return;
            }

            float marqueeWidth = availableWidth - MARQUEE_RIGHT_INSET;
            float overflow = text.preferredWidth - marqueeWidth;

            float travelTime = overflow / MARQUEE_SPEED;
            float cycleTime = travelTime + 2f * (MARQUEE_END_PAUSE + MARQUEE_FADE_DURATION);
            float phase = elapsed % cycleTime;
            float offset;
            float alpha = 1f;

            if (phase < MARQUEE_END_PAUSE)
            {
                offset = 0f;
            }
            else if (phase < MARQUEE_END_PAUSE + travelTime)
            {
                offset = (phase - MARQUEE_END_PAUSE) * MARQUEE_SPEED;
            }
            else if (phase < 2f * MARQUEE_END_PAUSE + travelTime)
            {
                offset = overflow;
            }
            else if (phase < 2f * MARQUEE_END_PAUSE + travelTime + MARQUEE_FADE_DURATION)
            {
                offset = overflow;
                float fadeProgress = (phase - 2f * MARQUEE_END_PAUSE - travelTime) / MARQUEE_FADE_DURATION;
                alpha = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
            }
            else
            {
                offset = 0f;
                float fadeProgress = (phase - 2f * MARQUEE_END_PAUSE - travelTime - MARQUEE_FADE_DURATION) /
                    MARQUEE_FADE_DURATION;
                alpha = Mathf.SmoothStep(0f, 1f, fadeProgress);
            }

            Rect bounds = text.rectTransform.rect;
            bounds.xMax -= MARQUEE_RIGHT_INSET;
            for (int i = 0; i < text.textInfo.characterCount; i++)
            {
                var character = text.textInfo.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                var meshInfo = text.textInfo.meshInfo[character.materialReferenceIndex];
                int vertexIndex = character.vertexIndex;
                var vertices = meshInfo.vertices;
                var uvs = meshInfo.uvs0;
                var colors = meshInfo.colors32;

                float originalLeft = vertices[vertexIndex].x;
                float originalRight = vertices[vertexIndex + 2].x;
                float shiftedLeft = originalLeft - offset;
                float shiftedRight = originalRight - offset;
                float width = originalRight - originalLeft;

                float clippedLeft = Mathf.Clamp(shiftedLeft, bounds.xMin, bounds.xMax);
                float clippedRight = Mathf.Clamp(shiftedRight, bounds.xMin, bounds.xMax);
                float leftT = width > 0f ? (clippedLeft - shiftedLeft) / width : 0f;
                float rightT = width > 0f ? (clippedRight - shiftedLeft) / width : 1f;

                Vector4 bottomLeftUv = uvs[vertexIndex];
                Vector4 topLeftUv = uvs[vertexIndex + 1];
                Vector4 topRightUv = uvs[vertexIndex + 2];
                Vector4 bottomRightUv = uvs[vertexIndex + 3];

                vertices[vertexIndex].x = clippedLeft;
                vertices[vertexIndex + 1].x = clippedLeft;
                vertices[vertexIndex + 2].x = clippedRight;
                vertices[vertexIndex + 3].x = clippedRight;
                uvs[vertexIndex] = Vector4.Lerp(bottomLeftUv, bottomRightUv, leftT);
                uvs[vertexIndex + 1] = Vector4.Lerp(topLeftUv, topRightUv, leftT);
                uvs[vertexIndex + 2] = Vector4.Lerp(topLeftUv, topRightUv, rightT);
                uvs[vertexIndex + 3] = Vector4.Lerp(bottomLeftUv, bottomRightUv, rightT);

                for (int vertexOffset = 0; vertexOffset < 4; vertexOffset++)
                {
                    int colorIndex = vertexIndex + vertexOffset;
                    var color = colors[colorIndex];
                    color.a = (byte) (color.a * alpha);
                    colors[colorIndex] = color;
                }
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Uv0 |
                TMP_VertexDataUpdateFlags.Colors32);
        }

        protected virtual void SetBackground(bool selected, BaseViewType.BackgroundType type)
        {
            NormalBackground.SetActive(false);
            SelectedBackground.SetActive(false);
            CategoryBackground.SetActive(false);

            switch (type)
            {
                case BaseViewType.BackgroundType.Normal:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        NormalBackground.SetActive(true);
                    }

                    break;
                case BaseViewType.BackgroundType.Category:
                    if (selected)
                    {
                        SelectedBackground.SetActive(true);
                    }
                    else
                    {
                        CategoryBackground.SetActive(true);
                    }

                    break;
            }
        }

#nullable enable
        protected void SetIcon(Sprite? sprite)
#nullable disable
        {
            _icon.sprite = sprite;
            _icon.gameObject.SetActive(sprite != null);
        }

        public void IconClick()
        {
            ViewType.IconClick();
        }
    }
}
