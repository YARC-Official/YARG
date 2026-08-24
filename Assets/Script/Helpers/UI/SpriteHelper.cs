using System.Collections.Generic;
using UnityEngine;

namespace YARG.Helpers.UI
{
    /// <summary>
    /// Generates and caches procedural UI sprites — pure-white shapes that
    /// multiply cleanly with any overlay color (unlike prefab sprites with
    /// baked-in textures). All sprites use Sliced borders, so callers
    /// control the final size via the Image's RectTransform.
    /// </summary>
    public static class SpriteHelper
    {
        private static readonly Dictionary<(int radius, int thickness), Sprite> _cache = new();

        /// <summary>
        /// Returns a cached pure-white rounded-rect sprite.
        ///
        /// <paramref name="radius"/> sets the corner radius (and Sliced
        /// border) in texture pixels.
        ///
        /// <paramref name="thickness"/> sets the border width. 0 produces
        /// a filled rect; a positive value produces a ring/outline of that
        /// width (transparent center). Use filled rects for tinted overlays
        /// and rings for outlines/borders.
        /// </summary>
        public static Sprite GetRoundedRect(int radius, int thickness = 0)
        {
            var key = (radius, thickness);
            if (!_cache.TryGetValue(key, out var sprite))
            {
                int size = radius * 2 + 4;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = $"RoundedRect_{radius}_{thickness}",
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        int cx = Mathf.Min(x, size - 1 - x);
                        int cy = Mathf.Min(y, size - 1 - y);

                        // Distance from the outer edge, going inward. In the
                        // corner region this follows the arc; elsewhere it's
                        // the straight distance to the nearest edge.
                        float outerDist;
                        if (cx < radius && cy < radius)
                        {
                            float dx = radius - cx;
                            float dy = radius - cy;
                            outerDist = radius - Mathf.Sqrt(dx * dx + dy * dy);
                        }
                        else
                        {
                            outerDist = Mathf.Min(cx, cy);
                        }

                        // Anti-alias the outer shape edge (1px)
                        float alpha = Mathf.Clamp01(outerDist + 0.5f);

                        if (thickness > 0)
                        {
                            // Ring — also fade out at the inner edge
                            float innerAA = Mathf.Clamp01(thickness + 0.5f - outerDist);
                            alpha = Mathf.Min(alpha, innerAA);
                        }

                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
                    }
                }

                tex.SetPixels32(pixels);
                tex.Apply();

                sprite = Sprite.Create(tex,
                    new Rect(0, 0, size, size),
                    new Vector2(0.5f, 0.5f), 100, 0,
                    SpriteMeshType.FullRect,
                    new Vector4(radius, radius, radius, radius));

                _cache[key] = sprite;
            }
            return sprite;
        }
    }
}
