using UnityEngine;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Keys;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class SustainLine : MonoBehaviour
    {
        private const float GLOW_THRESHOLD = 0.15f;

        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int _glowAmount = Shader.PropertyToID("_GlowAmount");

        private static readonly int _primaryAmplitude = Shader.PropertyToID("_PrimaryAmplitude");
        private static readonly int _secondaryAmplitude = Shader.PropertyToID("_SecondaryAmplitude");
        private static readonly int _tertiaryAmplitude = Shader.PropertyToID("_TertiaryAmplitude");
        private static readonly int _forwardOffset = Shader.PropertyToID("_ForwardOffset");

        [SerializeField]
        private Material _sustainMaterial;
        [SerializeField]
        private float _sustainWidth = 0.1f;
        [SerializeField]
        private int _subdivisions = 1; // Number of subdivisions on start/end edges
        [SerializeField]
        private bool _setShaderProperties = true;

        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private Mesh _sustainMesh;
        private Material _materialInstance;
        private TrackPlayer _player;

        private SustainState _hitState = SustainState.Waiting;
        private float _whammyFactor;

        private float _secondaryAmplitudeTime;
        private float _tertiaryAmplitudeTime;

        // Mesh properties
        private float _currentLength;
        private float _currentStartZ;

        private void Awake()
        {
            _player = GetComponentInParent<TrackPlayer>();

            // Setup mesh components
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshRenderer == null)
            {
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            _meshFilter = GetComponent<MeshFilter>();
            if (_meshFilter == null)
            {
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            // Create material instance
            if (_sustainMaterial != null)
            {
                _materialInstance = new Material(_sustainMaterial);
                _meshRenderer.material = _materialInstance;
            }

            // Create the mesh
            CreateSustainMesh();
        }

        private void Start()
        {
        }

        private void CreateSustainMesh()
        {
            _sustainMesh = new Mesh();
            _sustainMesh.name = "SustainLine";

            // Ensure subdivisions is at least 1
            int subdivisions = Mathf.Max(1, _subdivisions);

            // Calculate vertex count: (subdivisions + 1) vertices on each end edge
            int verticesPerEdge = subdivisions + 1;
            int totalVertices = verticesPerEdge * 2; // Start edge + end edge

            Vector3[] vertices = new Vector3[totalVertices];
            Vector3[] normals = new Vector3[totalVertices];
            Vector2[] uvs = new Vector2[totalVertices];

            // Calculate triangle count: subdivisions * 2 triangles per subdivision
            int triangleCount = subdivisions * 2;
            int[] triangles = new int[triangleCount * 3];

            // Set up UVs and normals
            for (int i = 0; i < totalVertices; i++)
            {
                normals[i] = Vector3.up;
            }

            // Set up UVs for start edge (right side in UV space)
            for (int i = 0; i < verticesPerEdge; i++)
            {
                float t = (float)i / subdivisions; // 0 to 1 across the width
                uvs[i] = new Vector2(1f, 1f - t); // Right side, top to bottom
            }

            // Set up UVs for end edge (left side in UV space)
            for (int i = 0; i < verticesPerEdge; i++)
            {
                float t = (float)i / subdivisions; // 0 to 1 across the width
                uvs[verticesPerEdge + i] = new Vector2(0f, 1f - t); // Left side, top to bottom
            }

            // Set up triangles with consistent winding
            int triangleIndex = 0;
            for (int i = 0; i < subdivisions; i++)
            {
                // Vertex indices for this quad segment
                int bottomLeft = i;                    // Start edge, left vertex
                int bottomRight = i + 1;              // Start edge, right vertex
                int topLeft = verticesPerEdge + i;    // End edge, left vertex
                int topRight = verticesPerEdge + i + 1; // End edge, right vertex

                // First triangle: bottomLeft -> topLeft -> bottomRight
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = bottomRight;

                // Second triangle: bottomRight -> topLeft -> topRight
                triangles[triangleIndex++] = bottomRight;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
            }

            _sustainMesh.vertices = vertices;
            _sustainMesh.normals = normals;
            _sustainMesh.uv = uvs;
            _sustainMesh.triangles = triangles;

            // Ensure proper bounds and validate mesh
            _sustainMesh.RecalculateNormals();
            _sustainMesh.RecalculateBounds();

            // Validate the mesh has the expected triangle count
            int expectedTriangles = subdivisions * 2 * 3;
            if (triangles.Length != expectedTriangles)
            {
                Debug.LogError($"SustainLine: Triangle count mismatch! Expected {expectedTriangles}, got {triangles.Length}");
            }

            _meshFilter.mesh = _sustainMesh;
        }

        public void Initialize(float len)
        {
            _currentLength = len;
            _currentStartZ = 0f;
            UpdateMeshGeometry();
            ResetAmplitudes();
        }

        public void SetState(SustainState state, Color c)
        {
            _hitState = state;

            if (_materialInstance == null) return;

            // Get the glow value based on the value of the color
            Color.RGBToHSV(c, out _, out _, out float value);
            float glow = Mathf.Max((GLOW_THRESHOLD - value) / GLOW_THRESHOLD, 0f);

            switch (state)
            {
                case SustainState.Waiting:
                    _materialInstance.color = c;
                    _materialInstance.SetColor(_emissionColor, c);
                    _materialInstance.SetFloat(_glowAmount, glow * 0.9f);
                    break;
                case SustainState.Hitting:
                    _materialInstance.color = c;
                    _materialInstance.SetColor(_emissionColor, c * 3f);
                    _materialInstance.SetFloat(_glowAmount, glow);
                    break;
                case SustainState.Missed:
                    _materialInstance.color = new Color(0f, 0f, 0f, 1f);
                    _materialInstance.SetColor(_emissionColor, new Color(0.1f, 0.1f, 0.1f, 1f));
                    _materialInstance.SetFloat(_glowAmount, 0f);
                    ResetAmplitudes();
                    break;
            }
        }

        private void ResetAmplitudes()
        {
            if (!_setShaderProperties || _materialInstance == null) return;

            _materialInstance.SetFloat(_primaryAmplitude, 0f);
            _materialInstance.SetFloat(_secondaryAmplitude, 0f);
            _materialInstance.SetFloat(_tertiaryAmplitude, 0f);

            _whammyFactor = 0f;

            _secondaryAmplitudeTime = 0f;
            _tertiaryAmplitudeTime = 0f;
        }

        public void UpdateSustainLine(float noteSpeed)
        {
            UpdateLengthForHit();
            UpdateAnimation(noteSpeed);
        }

        private void UpdateLengthForHit()
        {
            if (_hitState != SustainState.Hitting)
            {
                return;
            }

            // Get the new line start position. Said position should be at
            // the strike line and relative to the note itself.
            float newStart = -transform.parent.localPosition.z + TrackPlayer.STRIKE_LINE_POS;

            if (Mathf.Abs(_currentStartZ - newStart) > 0.001f)
            {
                _currentStartZ = newStart;
                UpdateMeshGeometry();
            }
        }

        private void UpdateAnimation(float noteSpeed)
        {
            // TODO: Reduce the amount of magic numbers lol

            if (!_setShaderProperties || _hitState != SustainState.Hitting || _materialInstance == null)
            {
                return;
            }

            // Update whammy factor
            if (_player is FiveFretGuitarPlayer guitarPlayer)
            {
                // Make sure to lerp it to prevent jumps
                _whammyFactor = Mathf.Lerp(_whammyFactor, guitarPlayer.WhammyFactor, Time.deltaTime * 6f);
            }

            float whammy = _whammyFactor * 1.5f;

            // Update the amplitude times
            _secondaryAmplitudeTime += Time.deltaTime * (4f + whammy);
            _tertiaryAmplitudeTime += Time.deltaTime * (1.7f + whammy);

            // Change line amplitude
            _materialInstance.SetFloat(_primaryAmplitude, 0.18f + whammy * 0.2f);
            _materialInstance.SetFloat(_secondaryAmplitude, Mathf.Sin(_secondaryAmplitudeTime) * (whammy + 0.5f));
            _materialInstance.SetFloat(_tertiaryAmplitude, Mathf.Sin(_tertiaryAmplitudeTime) * (whammy * 0.1f + 0.1f));

            // Move line forward
            float forwardSub = Time.deltaTime * 2.0f * (1f + whammy * 0.1f);
            _materialInstance.SetFloat(_forwardOffset, _materialInstance.GetFloat(_forwardOffset) + forwardSub);
        }

        private void UpdateMeshGeometry()
        {
            if (_sustainMesh == null) return;

            // Ensure subdivisions is at least 1
            int subdivisions = Mathf.Max(1, _subdivisions);
            int verticesPerEdge = subdivisions + 1;
            int totalVertices = verticesPerEdge * 2;

            Vector3[] vertices = new Vector3[totalVertices];
            Vector3[] normals = new Vector3[totalVertices];
            Vector2[] uvs = new Vector2[totalVertices];
            float halfWidth = _sustainWidth * 0.5f;

            // Create start edge vertices (at _currentStartZ)
            for (int i = 0; i < verticesPerEdge; i++)
            {
                float t = (float)i / subdivisions; // 0 to 1 across the width
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);
                vertices[i] = new Vector3(x, 0f, _currentStartZ);
                normals[i] = Vector3.up;
                uvs[i] = new Vector2(_currentLength - _currentStartZ, 1f - t);
            }

            // Create end edge vertices (at _currentLength)
            for (int i = 0; i < verticesPerEdge; i++)
            {
                float t = (float)i / subdivisions; // 0 to 1 across the width
                float x = Mathf.Lerp(-halfWidth, halfWidth, t);
                vertices[verticesPerEdge + i] = new Vector3(x, 0.01f, _currentLength); // Slightly elevated
                normals[verticesPerEdge + i] = Vector3.up;
                uvs[verticesPerEdge + i] = new Vector2(0f, 1f - t);
            }

            _sustainMesh.vertices = vertices;
            _sustainMesh.normals = normals;
            _sustainMesh.uv = uvs;
            _sustainMesh.RecalculateNormals();
            _sustainMesh.RecalculateBounds();
        }

        private void OnDestroy()
        {
            if (_materialInstance != null)
            {
                DestroyImmediate(_materialInstance);
            }

            if (_sustainMesh != null)
            {
                DestroyImmediate(_sustainMesh);
            }
        }
    }
}
