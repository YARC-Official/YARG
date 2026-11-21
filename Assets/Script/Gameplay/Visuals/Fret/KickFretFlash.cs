using UnityEngine;
using UnityEngine.Serialization;

namespace YARG.Gameplay.Visuals
{
    public class KickFretFlash : MonoBehaviour
    {
        private const float SECONDS_PER_FRAME = 1f / 50f;

        // Number of width segments used to build the curved flash mesh (see BuildArcMesh).
        private const int ARC_SEGMENTS = 20;

        // How tall the arc's bulge rests at when idle, as a multiple of the original flat
        // quad's half-height. The ends stay anchored at ARC_EDGE_LIFT_FACTOR above the quad's
        // original edges, while the middle lifts up further to clear the button row.
        private const float ARC_REST_HEIGHT_FACTOR = 0.25f;

        // How tall the arc's bulge gets pulled up to at the peak of a hit, like a string being
        // plucked upward from its middle, before springing back down to ARC_REST_HEIGHT_FACTOR.
        // This (rather than any change in width) is what reads as the "jump" on a hit.
        private const float ARC_HIT_HEIGHT_FACTOR = 0.45f;

        // Raises the arc's ends above the original flat quad's edges so the sides of the arc
        // clear the nearby button lights instead of sitting on top of them. Kept well under the
        // rest/hit height factors above so the bulge is still clearly taller in the middle.
        private const float ARC_EDGE_LIFT_FACTOR = 0.1f;

        // How wide the arc spans relative to the original flat quad's edges. Width no longer
        // animates (see the height/pull animation below), so this stays close to the original
        // full width so the ends still visibly reach the striker pads at the sides.
        private const float ARC_WIDTH_FACTOR = 1f;

        // Drives the arc's height/bulge across its whole visible lifetime: it starts at
        // ARC_REST_HEIGHT_FACTOR, gets pulled up to ARC_HIT_HEIGHT_FACTOR, then eases back down
        // to rest - noticeably faster than it was pulled up - and holds there for the rest of
        // the flash's fade.
        // How far into the flash's lifetime (as a 0-1 fraction) the pull-up hits its peak.
        private const float PULL_UP_TIME_FRACTION = 0.3f;
        // How far into the flash's lifetime the pull finishes easing back down to rest. Kept
        // much closer to PULL_UP_TIME_FRACTION than to 1 so the down-ease plays out over a
        // noticeably shorter span than the up-ease did.
        private const float PULL_DOWN_TIME_FRACTION = 0.5f;

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

        // Per-vertex data for the arc mesh, kept around so its bulge/height can be re-driven
        // every frame (see UpdateArcHeight) instead of only being baked once at build time.
        private Mesh _arcMesh;
        private Vector3[] _arcVertices;
        private float[] _arcUnitBulge;
        private float _arcEdgeLift;
        private float _arcRestAmplitude;
        private float _arcHitAmplitude;
        private float _arcBoundsMinY;
        private float _arcBoundsMaxY;

        private void Awake()
        {
            _flashMaterial = _kickFlashMesh.material;

            _currentSprite = _kickFlashTextures.Length - 1;
            UpdateTexture();

            _flashTransform = _kickFlashMesh.transform;
            _baseScale = _flashTransform.localScale;
            _flashDuration = Mathf.Max(_kickFlashTextures.Length * SECONDS_PER_FRAME, 0.0001f);
            // Start already past the end of the lifetime animation so the arc sits at its
            // resting (non-pulled) height before the first hit instead of starting pulled up.
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
            UpdateArcPull();
        }

        /// <summary>
        /// Animates the arc's height/bulge across its whole visible lifetime, like a string
        /// anchored at both ends being plucked upward from the middle (see
        /// ARC_REST_HEIGHT_FACTOR/ARC_HIT_HEIGHT_FACTOR/PULL_UP_TIME_FRACTION/
        /// PULL_DOWN_TIME_FRACTION above).
        /// </summary>
        private void UpdateArcPull()
        {
            float t = Mathf.Clamp01(_hitElapsedTime / _flashDuration);
            float amplitude;

            if (t < PULL_UP_TIME_FRACTION)
            {
                float localT = t / PULL_UP_TIME_FRACTION;
                float ease = 1f - (1f - localT) * (1f - localT);
                amplitude = Mathf.Lerp(_arcRestAmplitude, _arcHitAmplitude, ease);
            }
            else if (t < PULL_DOWN_TIME_FRACTION)
            {
                float localT = (t - PULL_UP_TIME_FRACTION) / (PULL_DOWN_TIME_FRACTION - PULL_UP_TIME_FRACTION);
                float ease = localT * localT;
                amplitude = Mathf.Lerp(_arcHitAmplitude, _arcRestAmplitude, ease);
            }
            else
            {
                amplitude = _arcRestAmplitude;
            }

            UpdateArcHeight(amplitude);
        }

        /// <summary>
        /// Re-positions the arc mesh's vertices for the given bulge amplitude (the amount the
        /// middle lifts above the edge-lifted sides), without rebuilding the mesh's
        /// triangles/UVs from scratch.
        /// </summary>
        private void UpdateArcHeight(float amplitude)
        {
            for (int i = 0; i <= ARC_SEGMENTS; i++)
            {
                float bulge = _arcEdgeLift + _arcUnitBulge[i] * amplitude;

                int bottom = i * 2;
                int top = bottom + 1;

                var bottomVertex = _arcVertices[bottom];
                bottomVertex.y = _arcBoundsMinY + bulge;
                _arcVertices[bottom] = bottomVertex;

                var topVertex = _arcVertices[top];
                topVertex.y = _arcBoundsMaxY + bulge;
                _arcVertices[top] = topVertex;
            }

            _arcMesh.vertices = _arcVertices;
            _arcMesh.RecalculateBounds();
        }

        public void PlayHitAnimation()
        {
            _updateTimer = 0f;
            _currentSprite = 0;
            UpdateTexture();

            _hitElapsedTime = 0f;
            UpdateArcPull();

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

            _arcEdgeLift = halfHeight * ARC_EDGE_LIFT_FACTOR;
            // Amplitudes are stored as the extra height above the edge lift, since that's the
            // part UpdateArcHeight scales by the per-vertex sine bulge below.
            _arcRestAmplitude = halfHeight * ARC_REST_HEIGHT_FACTOR - _arcEdgeLift;
            _arcHitAmplitude = halfHeight * ARC_HIT_HEIGHT_FACTOR - _arcEdgeLift;
            _arcBoundsMinY = bounds.min.y;
            _arcBoundsMaxY = bounds.max.y;

            _arcWorldHalfWidth = halfWidth * _baseScale.x;

            int vertexCount = (ARC_SEGMENTS + 1) * 2;
            _arcVertices = new Vector3[vertexCount];
            _arcUnitBulge = new float[ARC_SEGMENTS + 1];
            var uvs = new Vector2[vertexCount];

            for (int i = 0; i <= ARC_SEGMENTS; i++)
            {
                float t = (float) i / ARC_SEGMENTS;
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);

                // 0 at both edges, rising to 1 in the middle - this is what bends the ribbon
                // into an arc instead of a flat strip. The actual height is applied per-frame
                // by UpdateArcHeight, scaling this unit curve by the current pull amplitude, so
                // the bulge can animate (pulled up on a hit, then springing back to rest)
                // without rebuilding the mesh's triangles/UVs every frame. The ribbon keeps a
                // constant thickness along its whole length (rather than tapering it near the
                // ends) so its edges stay smooth and straight instead of reading as a
                // wobble/wiggle.
                _arcUnitBulge[i] = Mathf.Sin(Mathf.PI * t);

                int bottom = i * 2;
                int top = bottom + 1;

                _arcVertices[bottom] = new Vector3(x, 0f, 0f);
                _arcVertices[top] = new Vector3(x, 0f, 0f);

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
            // Hints to Unity that this mesh's vertex buffer will be updated frequently (every
            // frame, via UpdateArcHeight), rather than being authored once and left static.
            mesh.MarkDynamic();
            mesh.vertices = _arcVertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.mesh = mesh;
            _arcMesh = mesh;

            // Render both sides of the single-winding mesh above instead of duplicating its
            // triangles in both winding orders - with an additive-style material, drawing the
            // same coplanar surface twice (front + back) blends it twice, doubling its
            // brightness and making the glow visibly bleed/spread wider than intended.
            _flashMaterial.SetInt(CullShaderProperty, (int) UnityEngine.Rendering.CullMode.Off);

            // Set the mesh's initial shape to its resting (non-pulled) height.
            UpdateArcHeight(_arcRestAmplitude);
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