using TMPro;
using UnityEngine;

namespace YARG.Menu
{
    public sealed class TextMarquee
    {
        private const float SPEED = 70f;
        private const float END_PAUSE = 1f;
        private const float TRANSITION_FADE_DURATION = 0.25f;
        private const float RIGHT_INSET = 15f;
        private const float EDGE_FADE_EM = 0.6f;

        private readonly TextMeshProUGUI _text;

        private bool _active;
        private float _startTime;

        public TextMarquee(TextMeshProUGUI text)
        {
            _text = text;
        }

        public void SetActive(bool active)
        {
            _active = active;
            _startTime = Time.unscaledTime;

            _text.overflowMode = active ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            _text.ForceMeshUpdate();
        }

        public void Update()
        {
            if (!_active)
            {
                return;
            }

            UpdateMesh(Time.unscaledTime - _startTime);
        }

        private void UpdateMesh(float elapsed)
        {
            _text.ForceMeshUpdate();

            float availableWidth = _text.rectTransform.rect.width;
            if (_text.preferredWidth <= availableWidth)
            {
                return;
            }

            float marqueeWidth = availableWidth - RIGHT_INSET;
            float overflow = _text.preferredWidth - marqueeWidth;

            float travelTime = overflow / SPEED;
            float cycleTime = travelTime + 2f * (END_PAUSE + TRANSITION_FADE_DURATION);
            float phase = elapsed % cycleTime;
            float offset;
            float alpha = 1f;

            if (phase < END_PAUSE)
            {
                offset = 0f;
            }
            else if (phase < END_PAUSE + travelTime)
            {
                offset = (phase - END_PAUSE) * SPEED;
            }
            else if (phase < 2f * END_PAUSE + travelTime)
            {
                offset = overflow;
            }
            else if (phase < 2f * END_PAUSE + travelTime + TRANSITION_FADE_DURATION)
            {
                offset = overflow;
                float fadeProgress = (phase - 2f * END_PAUSE - travelTime) / TRANSITION_FADE_DURATION;
                alpha = 1f - Mathf.SmoothStep(0f, 1f, fadeProgress);
            }
            else
            {
                offset = 0f;
                float fadeProgress = (phase - 2f * END_PAUSE - travelTime - TRANSITION_FADE_DURATION) /
                    TRANSITION_FADE_DURATION;
                alpha = Mathf.SmoothStep(0f, 1f, fadeProgress);
            }

            Rect bounds = _text.rectTransform.rect;
            bounds.xMax -= RIGHT_INSET;
            float edgeFadeWidth = _text.fontSize * EDGE_FADE_EM;
            float leftFadeStrength = Mathf.Clamp01(offset / edgeFadeWidth);
            float rightFadeStrength = Mathf.Clamp01((overflow - offset) / edgeFadeWidth);
            for (int i = 0; i < _text.textInfo.characterCount; i++)
            {
                var character = _text.textInfo.characterInfo[i];
                if (!character.isVisible)
                {
                    continue;
                }

                var meshInfo = _text.textInfo.meshInfo[character.materialReferenceIndex];
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

                SetVertexAlpha(colors, vertexIndex, clippedLeft, bounds, alpha, edgeFadeWidth,
                    leftFadeStrength, rightFadeStrength);
                SetVertexAlpha(colors, vertexIndex + 1, clippedLeft, bounds, alpha, edgeFadeWidth,
                    leftFadeStrength, rightFadeStrength);
                SetVertexAlpha(colors, vertexIndex + 2, clippedRight, bounds, alpha, edgeFadeWidth,
                    leftFadeStrength, rightFadeStrength);
                SetVertexAlpha(colors, vertexIndex + 3, clippedRight, bounds, alpha, edgeFadeWidth,
                    leftFadeStrength, rightFadeStrength);
            }

            _text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Uv0 |
                TMP_VertexDataUpdateFlags.Colors32);
        }

        private static void SetVertexAlpha(Color32[] colors, int vertexIndex, float x, Rect bounds,
            float textAlpha, float edgeFadeWidth, float leftFadeStrength, float rightFadeStrength)
        {
            float leftFade = Mathf.Lerp(1f, Mathf.Clamp01((x - bounds.xMin) / edgeFadeWidth), leftFadeStrength);
            float rightFade = Mathf.Lerp(1f, Mathf.Clamp01((bounds.xMax - x) / edgeFadeWidth), rightFadeStrength);
            float edgeAlpha = Mathf.Min(leftFade, rightFade);

            var color = colors[vertexIndex];
            color.a = (byte) (color.a * textAlpha * edgeAlpha);
            colors[vertexIndex] = color;
        }
    }
}
