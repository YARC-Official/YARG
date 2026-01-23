using UnityEngine;
using YARG.Assets.Script.Gameplay.Player;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    public class SustainLine : MonoBehaviour
    {
        private const float MINIMUM_ALLOWED_LUMINANCE = 0.3f;
        private const float MISSED_NOTE_LUMINANCE = 0.25f;

        private static readonly Color MissedNoteColor = new(
            MISSED_NOTE_LUMINANCE,
            MISSED_NOTE_LUMINANCE,
            MISSED_NOTE_LUMINANCE,
            1f
        );

        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int IsActive      = Shader.PropertyToID("_IsActive");
        private static readonly int WhammyAmount  = Shader.PropertyToID("_WhammyAmount");

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

            Color color = GetBrighterColorIfTooDark(c);

            switch (state)
            {
                case SustainState.Waiting:
                    _materialInstance.color = color;
                    _materialInstance.SetColor(EmissionColor, color);
                    _materialInstance.SetInt(IsActive, 0);
                    break;
                case SustainState.Hitting:
                    _materialInstance.color = color;
                    _materialInstance.SetColor(EmissionColor, color * 3f);
                    _materialInstance.SetInt(IsActive, 1);
                    break;
                case SustainState.Missed:
                    _materialInstance.color = MissedNoteColor;
                    _materialInstance.SetColor(EmissionColor, MissedNoteColor * 0.4f);
                    _materialInstance.SetInt(IsActive, 0);
                    ResetAmplitudes();
                    break;
            }
        }

        private void ResetAmplitudes()
        {
            if (!_setShaderProperties || _materialInstance == null) return;

            _whammyFactor = 0f;
        }

        public void UpdateSustainLine()
        {
            UpdateLengthForHit();
            UpdateAnimation();
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

        private void UpdateAnimation()
        {
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

            // Update whammy factor
            if (_player is FiveLaneKeysPlayer keysPlayer)
            {
                // Make sure to lerp it to prevent jumps
                _whammyFactor = Mathf.Lerp(_whammyFactor, keysPlayer.WhammyFactor, Time.deltaTime * 6f);
            }

            // Change line amplitude
            _materialInstance.SetFloat(WhammyAmount, _whammyFactor);
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

        private static Color GetBrighterColorIfTooDark(Color color)
        {
            var resultingColor = new Color(color.r, color.g, color.b, color.a);

            // Magic numbers rooted in color theory
            double perceivedLuminance
                = 0.2126 * color.r
                + 0.7152 * color.g
                + 0.0722 * color.b;

            // No adjustment needed
            if (perceivedLuminance > MINIMUM_ALLOWED_LUMINANCE)
            {
                return resultingColor;
            }

            // In the case that the color is literally solid black
            if (perceivedLuminance <= 0f)
            {
                resultingColor.r = MINIMUM_ALLOWED_LUMINANCE;
                resultingColor.g = MINIMUM_ALLOWED_LUMINANCE;
                resultingColor.b = MINIMUM_ALLOWED_LUMINANCE;

                return resultingColor;
            }

            // If the color isn't literally black, we won't have a divide by 0 issue
            float scale = MINIMUM_ALLOWED_LUMINANCE / (float) perceivedLuminance;

            // Apply the scale uniformly
            resultingColor.r = Mathf.Clamp01(color.r * scale);
            resultingColor.g = Mathf.Clamp01(color.g * scale);
            resultingColor.b = Mathf.Clamp01(color.b * scale);

            return resultingColor;
        }
    }
}
