using System.Collections;
using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    /// <summary>
    /// A flat ring that expands outward from the center and fades out, used by Circular's kick
    /// animation. Built entirely procedurally (no reused/repositioned pre-authored asset) and
    /// meant to be parented directly under the button row's own transform (<c>FretArray</c>) -
    /// that space is already proven to correctly line up with the buttons (frets themselves are
    /// parented under it with only a local X offset), which sidesteps the coordinate-space
    /// guessing that made correctly positioning the old reused kick flash beam impossible to
    /// get right without Editor access.
    /// </summary>
    public class KickRingPulse : MonoBehaviour
    {
        private const int   SEGMENTS            = 48;
        private const float INNER_RADIUS_RATIO  = 0.68f;
        private const float START_RADIUS_RATIO  = 0.4f;
        // The ring's static shape is an asymmetric oval (baked directly into the mesh, not a
        // uniform transform scale, since a Transform's scale can't stretch +Z and -Z
        // differently) - squashed toward the highway side (local +Z, where incoming notes are
        // visible - squashing there avoids cluttering the player's view of upcoming notes) and
        // expanded toward the player side (local -Z, free space between the strike line and the
        // camera) to make better use of that otherwise-empty space.
        // NOTE: this assumes the Fret Array's local +Z axis points up the highway, away from the
        // player - if the ring instead ends up squashed on the wrong side in-engine, these two
        // values just need swapping.
        private const float FRONT_DEPTH_RATIO    = 0.5f;
        // Was bumped up to 1.7 earlier to make the back half read more noticeably, but that
        // ended up too prominent - brought back down to tone it down (was 1.2, then 1.5, 1.7).
        private const float BACK_DEPTH_RATIO     = 1.2f;
        // Boosts the ring's rendered brightness beyond its plain template color so it reads more
        // clearly as a distinct flash rather than blending into the buttons' own glow. Per-
        // instance (see Initialize) so the central back pulse can be dimmed independently of the
        // side pulses, which still default to this same value.
        private const float BRIGHTNESS_MULTIPLIER = 1.6f;

        // Trying out hiding the front half of the ring entirely (toward the highway/notes),
        // keeping only the back half (toward the player), since the front half was reportedly
        // making that area feel crowded. Flip back to false to restore the full front+back oval
        // exactly as it was - see BuildFullOvalRingMesh, left fully intact below for that purpose.
        private const bool BACK_HALF_ONLY = true;

        private MeshFilter   _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material     _material;
        private Color         _color;
        private Coroutine     _pulseCoroutine;

        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            _meshFilter = gameObject.AddComponent<MeshFilter>();
            _meshFilter.sharedMesh = BuildRingMesh();

            _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.enabled = false;
        }

        /// <summary>
        /// Reconfigures this instance (normally built as the main, central kick ring - see
        /// <see cref="BuildRingMesh"/>) into a smaller side accent instead, meant to sit just
        /// past the row's leftmost/rightmost button - a thin crescent/bracket (like "(" or ")")
        /// that exists entirely on the <paramref name="outwardSign"/> side (-1 for the left
        /// pulse, +1 for the right pulse) of its own origin, so scaling it up (see
        /// <see cref="PulseRoutine"/>) reads purely as a push outward, with nothing bulging back
        /// toward the row's center. Must be called once, right after construction, before the
        /// first <see cref="PlayPulse"/>.
        /// </summary>
        public void ConfigureAsSidePulse(float outwardSign)
        {
            _meshFilter.sharedMesh = BuildOutwardArcMesh(outwardSign);
        }

        /// <summary>
        /// Must be called once, before the first pulse. <paramref name="templateMaterial"/> is
        /// cloned (never shared) so this instance's own color/alpha can be animated
        /// independently - it should be an existing, already-correctly-configured
        /// transparent/glowing particle material from this same theme (see
        /// <see cref="FretArray.GetHitEffectTemplateMaterial"/>), so the pulse automatically
        /// matches the theme's established glow look instead of relying on a brand new,
        /// unverified shader setup. <paramref name="brightnessMultiplier"/> defaults to the
        /// shared <see cref="BRIGHTNESS_MULTIPLIER"/> but can be lowered per-instance (e.g. to
        /// dim the central back pulse without affecting the side pulses).
        /// </summary>
        public void Initialize(Material templateMaterial, Color color, float brightnessMultiplier = BRIGHTNESS_MULTIPLIER)
        {
            _material = templateMaterial != null ? new Material(templateMaterial) : new Material(Shader.Find("Sprites/Default"));
            _meshRenderer.material = _material;

            var alpha = color.a;
            _color = color * brightnessMultiplier;
            _color.a = alpha;
        }

        /// <summary>
        /// Expands from a small starting radius up to <paramref name="maxRadius"/> while fading
        /// out, over <paramref name="duration"/> seconds. Safe to call again before a previous
        /// pulse finishes - it simply restarts.
        /// </summary>
        public void PlayPulse(float duration, float maxRadius)
        {
            if (_pulseCoroutine != null)
            {
                StopCoroutine(_pulseCoroutine);
            }

            _pulseCoroutine = StartCoroutine(PulseRoutine(duration, maxRadius));
        }

        private IEnumerator PulseRoutine(float duration, float maxRadius)
        {
            _meshRenderer.enabled = true;

            var startRadius = maxRadius * START_RADIUS_RATIO;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                var t = elapsed / duration;
                var radius = Mathf.Lerp(startRadius, maxRadius, t);
                // Uniform scale - the oval's asymmetric front/back aspect ratio is already baked
                // into the mesh itself (see BuildRingMesh), so growing it uniformly preserves
                // that shape as it expands instead of distorting it further.
                transform.localScale = new Vector3(radius, 1f, radius);
                ApplyAlpha(1f - t);

                elapsed += Time.deltaTime;
                yield return null;
            }

            _meshRenderer.enabled = false;
            _pulseCoroutine = null;
        }

        private void ApplyAlpha(float alpha)
        {
            var c = _color;
            c.a *= alpha;

            _material.color = c;
            if (_material.HasProperty(_emissionColor))
            {
                _material.SetColor(_emissionColor, c);
            }
        }

        private static Mesh BuildRingMesh()
        {
            return BACK_HALF_ONLY ? BuildBackHalfRingMesh() : BuildFullOvalRingMesh();
        }

        // Builds just the back half (toward the player) as an open arc, instead of a closed
        // oval - both ends land exactly on the local X axis (Z == 0, flush with the fret line),
        // since that's where BuildFullOvalRingMesh's own front/back halves already met
        // seamlessly, so there's no gap/seam introduced by cutting the front half away here.
        private static Mesh BuildBackHalfRingMesh()
        {
            var mesh = new Mesh { name = "KickRingPulseMesh" };

            const int halfSegments = SEGMENTS / 2;
            var vertices = new Vector3[(halfSegments + 1) * 2];
            var uvs = new Vector2[(halfSegments + 1) * 2];
            var triangles = new int[halfSegments * 12];

            for (int i = 0; i <= halfSegments; i++)
            {
                // Sweep only from PI to 2*PI (the back half, where sin <= 0) - every vertex here
                // uses BACK_DEPTH_RATIO unconditionally, since there's no front half left to
                // switch to.
                float angle = Mathf.PI + i / (float) halfSegments * Mathf.PI;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * INNER_RADIUS_RATIO, 0f, sin * INNER_RADIUS_RATIO * BACK_DEPTH_RATIO);
                vertices[i * 2 + 1] = new Vector3(cos, 0f, sin * BACK_DEPTH_RATIO);

                uvs[i * 2] = new Vector2(i / (float) halfSegments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float) halfSegments, 1f);

                // The last vertex pair has no "next" segment - this is an open strip, not a
                // closed loop, so it doesn't wrap back around to vertex 0 like the full oval does.
                if (i == halfSegments)
                {
                    continue;
                }

                int next = i + 1;
                int triBase = i * 12;

                triangles[triBase] = i * 2;
                triangles[triBase + 1] = next * 2;
                triangles[triBase + 2] = i * 2 + 1;

                triangles[triBase + 3] = next * 2;
                triangles[triBase + 4] = next * 2 + 1;
                triangles[triBase + 5] = i * 2 + 1;

                // Same two triangles again, reversed winding, so the ring renders regardless of
                // which winding order this shader/orientation treats as front-facing.
                triangles[triBase + 6] = i * 2;
                triangles[triBase + 7] = i * 2 + 1;
                triangles[triBase + 8] = next * 2;

                triangles[triBase + 9] = next * 2;
                triangles[triBase + 10] = i * 2 + 1;
                triangles[triBase + 11] = next * 2 + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
            mesh.RecalculateBounds();

            return mesh;
        }

        // Builds a flat annulus (ring) in the local XZ plane, normal facing local +Y - the same
        // convention the buttons themselves use (parented under FretArray with no rotation, at
        // local Y = 0 sitting flush on the highway surface). Only the outer radius needs
        // animating afterwards (via transform.localScale), since the inner/outer ratio is baked
        // into the mesh itself. Kept fully intact (unused while BACK_HALF_ONLY is true) so
        // reverting to the full oval is just a one-line flag flip away.
        private static Mesh BuildFullOvalRingMesh()
        {
            var mesh = new Mesh { name = "KickRingPulseMesh" };

            var vertices = new Vector3[SEGMENTS * 2];
            var uvs = new Vector2[SEGMENTS * 2];
            // Double-sided (both winding orders emitted per segment) - without Editor access to
            // verify which winding ends up front-facing for this local orientation, this avoids
            // any risk of the ring being invisible from the player's camera due to back-face
            // culling. Negligible cost for a single low-poly ring mesh.
            var triangles = new int[SEGMENTS * 12];

            for (int i = 0; i < SEGMENTS; i++)
            {
                float angle = i / (float) SEGMENTS * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);
                // Picks which side of the oval this vertex belongs to - see the FRONT/BACK
                // constants above. Safe to use a hard switch (no smoothing) since both sides
                // meet exactly at sin == 0, where this multiplier has no effect either way, so
                // there's no visible seam/gap at the transition.
                float zRatio = sin >= 0f ? FRONT_DEPTH_RATIO : BACK_DEPTH_RATIO;

                vertices[i * 2] = new Vector3(cos * INNER_RADIUS_RATIO, 0f, sin * INNER_RADIUS_RATIO * zRatio);
                vertices[i * 2 + 1] = new Vector3(cos, 0f, sin * zRatio);

                uvs[i * 2] = new Vector2(i / (float) SEGMENTS, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float) SEGMENTS, 1f);

                int next = (i + 1) % SEGMENTS;
                int triBase = i * 12;

                triangles[triBase] = i * 2;
                triangles[triBase + 1] = next * 2;
                triangles[triBase + 2] = i * 2 + 1;

                triangles[triBase + 3] = next * 2;
                triangles[triBase + 4] = next * 2 + 1;
                triangles[triBase + 5] = i * 2 + 1;

                // Same two triangles again, reversed winding, so the ring renders regardless of
                // which winding order this shader/orientation treats as front-facing.
                triangles[triBase + 6] = i * 2;
                triangles[triBase + 7] = i * 2 + 1;
                triangles[triBase + 8] = next * 2;

                triangles[triBase + 9] = next * 2;
                triangles[triBase + 10] = i * 2 + 1;
                triangles[triBase + 11] = next * 2 + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            // Explicit flat-up normals instead of RecalculateNormals() - with both winding
            // orders sharing the same vertices (see the double-sided comment above), averaging
            // per-vertex face normals would partially cancel out to a near-zero vector instead
            // of a clean (0, 1, 0), which could break lighting/shading on some shaders.
            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
            mesh.RecalculateBounds();

            return mesh;
        }

        // Unlike BuildSideSweepMesh (removed - it was a full 360-degree oval, which still bulged
        // a bit toward the row's center as it scaled up, reading as "opening in the middle"
        // rather than a clean push outward), this only builds the half of the ring that's
        // already entirely on the outward side to begin with (cos * outwardSign >= 0, i.e.
        // every vertex's local-X sits at or past the pulse's own origin, never dipping back
        // toward the row's center) - a thin crescent/bracket band, open on the inward side,
        // bulging out toward outwardSign (-1 for the left pulse's "(" shape, +1 for the right
        // pulse's ")" shape). Scaling this up from near-zero (see PulseRoutine) then reads as
        // the two lines sliding outward, since the whole shape already only exists on that one
        // side of its own origin.
        private static Mesh BuildOutwardArcMesh(float outwardSign)
        {
            var mesh = new Mesh { name = "KickSidePulseMesh" };

            const int halfSegments = SEGMENTS / 2;
            var vertices = new Vector3[(halfSegments + 1) * 2];
            var uvs = new Vector2[(halfSegments + 1) * 2];
            var triangles = new int[halfSegments * 12];

            for (int i = 0; i <= halfSegments; i++)
            {
                // Sweeps from -90 to +90 degrees around local +X (outwardSign > 0) or local -X
                // (outwardSign < 0), so cos is always >= 0 relative to outwardSign across the
                // whole arc, and the two open ends land exactly on the local Z axis (X == 0,
                // flush with the pulse's own origin/the button's edge) rather than bulging past
                // it toward the row's center.
                float angle = outwardSign >= 0f
                    ? -Mathf.PI / 2f + i / (float) halfSegments * Mathf.PI
                    : Mathf.PI / 2f + i / (float) halfSegments * Mathf.PI;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * INNER_RADIUS_RATIO, 0f, sin * INNER_RADIUS_RATIO);
                vertices[i * 2 + 1] = new Vector3(cos, 0f, sin);

                uvs[i * 2] = new Vector2(i / (float) halfSegments, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float) halfSegments, 1f);

                // The last vertex pair has no "next" segment - this is an open strip, not a
                // closed loop.
                if (i == halfSegments)
                {
                    continue;
                }

                int next = i + 1;
                int triBase = i * 12;

                triangles[triBase] = i * 2;
                triangles[triBase + 1] = next * 2;
                triangles[triBase + 2] = i * 2 + 1;

                triangles[triBase + 3] = next * 2;
                triangles[triBase + 4] = next * 2 + 1;
                triangles[triBase + 5] = i * 2 + 1;

                // Same two triangles again, reversed winding, so the ring renders regardless of
                // which winding order this shader/orientation treats as front-facing.
                triangles[triBase + 6] = i * 2;
                triangles[triBase + 7] = i * 2 + 1;
                triangles[triBase + 8] = next * 2;

                triangles[triBase + 9] = next * 2;
                triangles[triBase + 10] = i * 2 + 1;
                triangles[triBase + 11] = next * 2 + 1;
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;

            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
