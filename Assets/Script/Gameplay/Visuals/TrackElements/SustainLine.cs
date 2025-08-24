using UnityEngine;
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

            // Create vertices for a quad (4 vertices)
            Vector3[] vertices = new Vector3[4];
            Vector3[] normals = new Vector3[4];
            Vector2[] uvs = new Vector2[4];
            int[] triangles = new int[6];

            // Set up triangles (two triangles forming a quad) - ensure correct winding order
            triangles[0] = 0; triangles[1] = 2; triangles[2] = 1;
            triangles[3] = 0; triangles[4] = 3; triangles[5] = 2;

            // Set up UVs - alternative rotation fix
            uvs[0] = new Vector2(1, 0); // Bottom left -> Bottom right in UV space
            uvs[1] = new Vector2(1, 1); // Bottom right -> Top right in UV space
            uvs[2] = new Vector2(0, 1); // Top right -> Top left in UV space
            uvs[3] = new Vector2(0, 0); // Top left -> Bottom left in UV space

            // Set up normals (pointing up)
            for (int i = 0; i < 4; i++)
            {
                normals[i] = Vector3.up;
            }

            _sustainMesh.vertices = vertices;
            _sustainMesh.normals = normals;
            _sustainMesh.uv = uvs;
            _sustainMesh.triangles = triangles;

            // Ensure proper bounds
            _sustainMesh.RecalculateBounds();

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
            if (_player is FiveFretPlayer player)
            {
                // Make sure to lerp it to prevent jumps
                _whammyFactor = Mathf.Lerp(_whammyFactor, player.WhammyFactor, Time.deltaTime * 6f);
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
            float forwardSub = Time.deltaTime * noteSpeed / 2.5f * (1f + whammy * 0.1f);
            _materialInstance.SetFloat(_forwardOffset, _materialInstance.GetFloat(_forwardOffset) + forwardSub);
        }

        private void UpdateMeshGeometry()
        {
            if (_sustainMesh == null) return;

            Vector3[] vertices = new Vector3[4];
            Vector3[] normals = new Vector3[4];
            float halfWidth = _sustainWidth * 0.5f;

            // Create quad vertices from current start to full length
            // Bottom vertices (at _currentStartZ)
            vertices[0] = new Vector3(-halfWidth, 0f, _currentStartZ); // Bottom left
            vertices[1] = new Vector3(halfWidth, 0f, _currentStartZ);  // Bottom right

            // Top vertices (at _currentLength)
            vertices[2] = new Vector3(halfWidth, 0.01f, _currentLength);  // Top right (slightly elevated)
            vertices[3] = new Vector3(-halfWidth, 0.01f, _currentLength); // Top left (slightly elevated)

            // Set normals
            for (int i = 0; i < 4; i++)
            {
                normals[i] = Vector3.up;
            }

            _sustainMesh.vertices = vertices;
            _sustainMesh.normals = normals;
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
