using UnityEngine;
using UnityEngine.Serialization;

namespace YARG.Gameplay.Visuals
{
    public class KickFretFlash : MonoBehaviour
    {
        private const float SECONDS_PER_FRAME = 1f / 50f;

        // Number of width segments used to build the curved flash mesh (see BuildArcMesh).
        // Higher = smoother curve, at the cost of a few more triangles for a tiny effect mesh.
        private const int ARC_SEGMENTS = 20;

        // How tall the arc's bulge gets at its midpoint, as a multiple of the original flat
        // quad's half-height. The ends of the arc stay anchored at the quad's original edges
        // (so it still lines up with the kick frets at the sides), while the middle lifts up
        // and clears the button row instead of lying flat across it. Kept fairly low (rather
        // than a tall, dramatic arc) so it stays close to the button row instead of bulging
        // way up above it.
        private const float ARC_HEIGHT_FACTOR = 0.25f;

        // Shrinks the arc inward from the original flat quad's edges so its resting width
        // doesn't additively overlap (and visually "bleed into") the striker pads' own glow.
        // The width-lifetime animation below (see WIDTH_PEAK_SCALE) still stretches it out
        // wider for a moment, so it only reaches all the way out to the pads then.
        private const float ARC_WIDTH_FACTOR = 0.85f;

        // Drives the arc's width across its whole visible lifetime (not just the instant of
        // the hit): it starts narrow, then grows out wider as the flash's curve/bulge is
        // "jumping" into view, and holds there while the flash fades out via its texture
        // flipbook - rather than easing back down narrow - so the arc only ever grows and
        // disappears, instead of visibly shrinking back down first.
        private const float WIDTH_START_SCALE = 0.55f;
        private const float WIDTH_PEAK_SCALE = 1.2f;
        // How far into the flash's lifetime (as a 0-1 fraction) the width hits its peak.
        private const float WIDTH_PEAK_TIME_FRACTION = 0.3f;

        // Sparks/embers that burst out when the kick is hit.
        private const int SPARK_BURST_COUNT = 10;
        private const float SPARK_LIFETIME = 0.35f;
        private const float SPARK_MIN_SPEED = 0.35f;
        private const float SPARK_MAX_SPEED = 0.85f;
        private const float SPARK_MIN_SIZE = 0.02f;
        private const float SPARK_MAX_SIZE = 0.045f;
        private const float SPARK_GRAVITY = 1.2f;
        // Boosts the spark color past the flash's plain tint into HDR range, so the embers
        // read as bright/hot rather than a flat, capped-white color (same technique used for
        // the drum hit Flash particle - see Fret.ApplyDrumHitEffectTuning).
        private const float SPARK_COLOR_BOOST = 1.6f;

        private static readonly int CullShaderProperty = Shader.PropertyToID("_Cull");

        [SerializeField]
        private MeshRenderer _kickFlashMesh;
        [FormerlySerializedAs("_textures")]
        [SerializeField]
        private Texture2D[] _kickFlashTextures;

        // Material used to render the spark/ember particles. Assign an additive/unlit
        // particle material (e.g. the shared ParticlesUnlit material used by the drum hit
        // particles) in the prefab.
        [SerializeField]
        private Material _sparkMaterial;

        private Material _flashMaterial;
        private int _currentSprite;
        private float _updateTimer;

        private Transform _flashTransform;
        private Vector3 _baseScale;
        private float _hitElapsedTime;
        private float _flashDuration;

        private ParticleSystem _sparkParticles;
        private float _arcWorldHalfWidth;

        private void Awake()
        {
            _flashMaterial = _kickFlashMesh.material;

            _currentSprite = _kickFlashTextures.Length - 1;
            UpdateTexture();

            _flashTransform = _kickFlashMesh.transform;
            _baseScale = _flashTransform.localScale;
            _flashDuration = Mathf.Max(_kickFlashTextures.Length * SECONDS_PER_FRAME, 0.0001f);
            // Start already past the end of the lifetime animation so the arc sits at its
            // narrow, resting width before the first hit instead of starting at full width.
            _hitElapsedTime = _flashDuration;

            BuildArcMesh();
            CreateSparkParticles();
        }

        public void Initialize(Color flash)
        {
            _flashMaterial.color = flash;

            if (_sparkParticles != null)
            {
                var main = _sparkParticles.main;
                main.startColor = new Color(
                    flash.r * SPARK_COLOR_BOOST,
                    flash.g * SPARK_COLOR_BOOST,
                    flash.b * SPARK_COLOR_BOOST,
                    1f);
            }
        }

        private void UpdateTexture()
        {
            _flashMaterial.mainTexture = _kickFlashTextures[_currentSprite];
        }

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            while (_updateTimer >= SECONDS_PER_FRAME && _currentSprite < _kickFlashTextures.Length)
            {
                _updateTimer -= SECONDS_PER_FRAME;
                UpdateTexture();
                _currentSprite++;
            }

            _hitElapsedTime += Time.deltaTime;
            UpdateArcWidth();
        }

        /// <summary>
        /// Animates the arc's width across its whole visible lifetime: narrow at the start,
        /// widening out to its peak partway through (as if the curve is "jumping" up into
        /// view), then holding there while the flash fades out via its texture flipbook. See
        /// WIDTH_START_SCALE/WIDTH_PEAK_SCALE above.
        /// </summary>
        private void UpdateArcWidth()
        {
            float t = Mathf.Clamp01(_hitElapsedTime / _flashDuration);
            float scale;

            if (t < WIDTH_PEAK_TIME_FRACTION)
            {
                float localT = t / WIDTH_PEAK_TIME_FRACTION;
                float ease = 1f - (1f - localT) * (1f - localT);
                scale = Mathf.Lerp(WIDTH_START_SCALE, WIDTH_PEAK_SCALE, ease);
            }
            else
            {
                scale = WIDTH_PEAK_SCALE;
            }

            _flashTransform.localScale = new Vector3(_baseScale.x * scale, _baseScale.y, _baseScale.z);
        }

        public void PlayHitAnimation()
        {
            _updateTimer = 0f;
            _currentSprite = 0;
            UpdateTexture();

            _hitElapsedTime = 0f;
            UpdateArcWidth();

            if (_sparkParticles != null)
            {
                _sparkParticles.Clear();
                _sparkParticles.Play();
            }
        }

        /// <summary>
        /// Replaces the flat quad mesh with a curved ribbon that bulges upward toward its
        /// middle (like a rainbow/banana arc), while keeping the same edges/width as the
        /// original flat quad so it still lines up with the kick frets at the sides. This is
        /// what keeps the flash from lying flat across (and behind) the button row.
        /// </summary>
        private void BuildArcMesh()
        {
            var meshFilter = _kickFlashMesh.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null) return;

            // Derive the curve's width/height from the original mesh's own bounds, rather than
            // assuming a fixed size, so this keeps working if the source quad is ever resized.
            var bounds = meshFilter.sharedMesh.bounds;
            float halfWidth = bounds.extents.x * ARC_WIDTH_FACTOR;
            float halfHeight = bounds.extents.y;
            float arcHeight = halfHeight * ARC_HEIGHT_FACTOR;

            _arcWorldHalfWidth = halfWidth * _baseScale.x;

            int vertexCount = (ARC_SEGMENTS + 1) * 2;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];

            for (int i = 0; i <= ARC_SEGMENTS; i++)
            {
                float t = (float) i / ARC_SEGMENTS;
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);

                // Zero at both edges, peaking in the middle - this is what bends the ribbon
                // into an arc instead of a flat strip. The ribbon keeps a constant thickness
                // along its whole length (rather than tapering it near the ends) so its edges
                // stay smooth and straight instead of reading as a wobble/wiggle.
                float bulge = arcHeight * Mathf.Sin(Mathf.PI * t);

                int bottom = i * 2;
                int top = bottom + 1;

                vertices[bottom] = new Vector3(x, bounds.min.y + bulge, 0f);
                vertices[top] = new Vector3(x, bounds.max.y + bulge, 0f);

                uvs[bottom] = new Vector2(t, 0f);
                uvs[top] = new Vector2(t, 1f);
            }

            var triangles = new int[ARC_SEGMENTS * 6];
            int triIndex = 0;
            for (int i = 0; i < ARC_SEGMENTS; i++)
            {
                int b0 = i * 2;
                int t0 = b0 + 1;
                int b1 = b0 + 2;
                int t1 = b0 + 3;

                triangles[triIndex++] = b0;
                triangles[triIndex++] = t0;
                triangles[triIndex++] = b1;
                triangles[triIndex++] = t0;
                triangles[triIndex++] = t1;
                triangles[triIndex++] = b1;
            }

            var mesh = new Mesh { name = "KickFlashArc" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;

            // Render both sides of the single-winding mesh above instead of duplicating its
            // triangles in both winding orders - with an additive-style material, drawing the
            // same coplanar surface twice (front + back) blends it twice, doubling its
            // brightness and making the glow visibly bleed/spread wider than intended.
            _flashMaterial.SetInt(CullShaderProperty, (int) UnityEngine.Rendering.CullMode.Off);
        }

        /// <summary>
        /// Builds a quick burst of small spark/ember particles that fire outward and fall
        /// along the arc when the kick is hit, similar to the embers shed by RB4's kick flash.
        /// Built entirely at runtime (rather than a hand-authored particle prefab) so its
        /// tuning lives in one reviewable place alongside the arc mesh above.
        /// </summary>
        private void CreateSparkParticles()
        {
            if (_sparkMaterial == null) return;

            var sparkObject = new GameObject("Kick Flash Sparks");
            // Parented to the (unscaled, identity-rotation) grandparent rather than the flash
            // mesh itself, so the flash's non-uniform scale/pop-scale animation doesn't distort
            // the particles - position/rotation are copied once instead, since the flash never
            // moves at runtime.
            sparkObject.transform.SetParent(transform.parent, false);
            sparkObject.transform.position = transform.position;
            sparkObject.transform.rotation = transform.rotation;

            _sparkParticles = sparkObject.AddComponent<ParticleSystem>();
            // AddComponent<ParticleSystem> starts the system playing immediately, and several
            // of the main-module properties set below (e.g. duration) can't be changed while
            // playing - stop (and clear) it first so configuration doesn't throw.
            _sparkParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var renderer = sparkObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = _sparkMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var main = _sparkParticles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = SPARK_LIFETIME;
            main.startLifetime = SPARK_LIFETIME;
            main.startSpeed = new ParticleSystem.MinMaxCurve(SPARK_MIN_SPEED, SPARK_MAX_SPEED);
            main.startSize = new ParticleSystem.MinMaxCurve(SPARK_MIN_SIZE, SPARK_MAX_SIZE);
            main.gravityModifier = SPARK_GRAVITY;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _sparkParticles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short) SPARK_BURST_COUNT) });

            var shape = _sparkParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_arcWorldHalfWidth * 2f, 0.01f, 0.01f);
            shape.randomDirectionAmount = 0.4f;

            var colorOverLifetime = _sparkParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;
        }
    }
}