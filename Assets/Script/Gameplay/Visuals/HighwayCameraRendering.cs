using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class HighwayCameraRendering : MonoBehaviour
    {
        public const int MAX_MATRICES = 128;

        [SerializeField]
        private RawImage _highwaysOutput;

        private List<TrackPlayer> _players = new();
        private List<Vector2> _fadeParams = new();
        private List<float> _curveFactors = new();

        private Camera _renderCamera;
        private RenderTexture _highwaysOutputTexture;

        private Matrix4x4[] _camViewMatrices = null;
        private Matrix4x4[] _camInvViewMatrices = null;
        private Matrix4x4[] _camProjMatrices = null;
        private float _scale = 1.0f;

        public static readonly int YargHighwaysNumberID = Shader.PropertyToID("_YargHighwaysN");
        public static readonly int YargHighwayCamViewMatricesID = Shader.PropertyToID("_YargCamViewMatrices");
        public static readonly int YargHighwayCamInvViewMatricesID = Shader.PropertyToID("_YargCamInvViewMatrices");
        public static readonly int YargHighwayCamProjMatricesID = Shader.PropertyToID("_YargCamProjMatrices");
        public static readonly int YargCurveFactorsID = Shader.PropertyToID("_YargCurveFactors");
        public static readonly int YargFadeParamsID = Shader.PropertyToID("_YargFadeParams");

        public RenderTexture GetHighwayOutputTexture()
        {
            if (_highwaysOutputTexture == null)
            {
                // Set up render texture
                var descriptor = new RenderTextureDescriptor(
                    Screen.width, Screen.height,
                    RenderTextureFormat.ARGBHalf);
                descriptor.mipCount = 0;
                _highwaysOutputTexture = new RenderTexture(descriptor);
                _highwaysOutput.texture = _highwaysOutputTexture;
            }
            return _highwaysOutputTexture;
        }

        private void RecalculateCameraBounds()
        {
            float maxWorld = float.NaN;
            float minWorld = float.NaN;
            foreach (var player in _players)
            {
                // This doesn't matter too much as long
                // as everything fits. This is just for frustrum culling.
                var x = player.transform.position.x;
                if (float.IsNaN(maxWorld) || maxWorld < x + 1)
                {
                    maxWorld = x + 1;
                }
                if (float.IsNaN(minWorld) || minWorld > x - 1)
                {
                    minWorld = x - 1;
                }
            }
            _renderCamera.transform.position = _renderCamera.transform.position.WithX((minWorld + maxWorld) / 2);
            _renderCamera.orthographicSize = Math.Max(25, (maxWorld - minWorld) / 2);
        }

        public void AddTrackPlayer(TrackPlayer trackPlayer)
        {
            var cameraData = trackPlayer.TrackCamera.GetUniversalAdditionalCameraData();
            // This effectively disables rendering it but keeps components active
            cameraData.renderType = CameraRenderType.Overlay;
            _players.Add(trackPlayer);
            RecalculateCameraBounds();

            // This equation calculates a good scale for all of the tracks.
            // It was made with experimentation; there's probably a "real" formula for this.
            _scale = Mathf.Max(0.7f * Mathf.Log10(_players.Count - 1), 0f);
            _scale = 1f - _scale;
        }


        private void Awake()
        {
        }

        private void OnEnable()
        {
            _renderCamera = GetComponent<Camera>();
            _renderCamera.targetTexture = GetHighwayOutputTexture();

            Shader.SetGlobalInteger(YargHighwaysNumberID, 0);
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnPreCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
        }

        private void OnEndCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }
            Shader.SetGlobalInteger(YargHighwaysNumberID, 0);
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            if (_players.Count == 0)
            {
                return;
            }

            if (_camProjMatrices == null)
            {
                _camProjMatrices = new Matrix4x4[MAX_MATRICES];
            }
            if (_camViewMatrices == null)
            {
                _camViewMatrices = new Matrix4x4[MAX_MATRICES];
            }
            if (_camInvViewMatrices == null)
            {
                _camInvViewMatrices = new Matrix4x4[MAX_MATRICES];
            }

            for (int i = 0; i < _players.Count; ++i)
            {
                var camera = _players[i].TrackCamera;
                _camViewMatrices[i] = camera.worldToCameraMatrix;
                _camInvViewMatrices[i] = camera.cameraToWorldMatrix;
                var projMatrix = GetModifiedProjectionMatrix(camera.projectionMatrix,
                                                             i, _players.Count, _scale);
                _camProjMatrices[i] = GL.GetGPUProjectionMatrix(projMatrix, SystemInfo.graphicsUVStartsAtTop /* if we're not rendering to render texture this has to be changed to always false */);
            }
            Shader.SetGlobalMatrixArray(YargHighwayCamViewMatricesID, _camViewMatrices);
            Shader.SetGlobalMatrixArray(YargHighwayCamInvViewMatricesID, _camInvViewMatrices);
            Shader.SetGlobalMatrixArray(YargHighwayCamProjMatricesID, _camProjMatrices);
            Shader.SetGlobalInteger(YargHighwaysNumberID, _players.Count);
        }

        /// <summary>
        /// Builds a post-projection matrix that applies NDC-space scaling and offset,
        /// used to tile multiple viewports side-by-side in clip space.
        /// </summary>
        /// <param name="index">The index of the highway [0, N-1]</param>
        /// <param name="highwayCount">Total number of highways (N)</param>
        /// <param name="highwayScale">Scale of each highway in NDC (e.g. 1.0 means full size)</param>
        public static Matrix4x4 GetPostProjectionMatrix(int index, int highwayCount, float highwayScale)
        {
            if (highwayCount < 1)
                return Matrix4x4.identity;

            // Divide screen into N equal regions: [-1, 1] => 2.0 width
            float laneWidth = 2.0f / highwayCount; // NDC horizontal span is [-1, 1] → 2.0
            float centerX = -1.0f + laneWidth * (index + 0.5f);
            float offsetX = centerX;
            float offsetY = - 1.0f + highwayScale; // Offset down if scaled vertically

            // This matrix modifies the output of clip space before perspective divide
            // Performs: clip.xy = clip.xy * scale + offset * clip.w
            Matrix4x4 postProj = Matrix4x4.identity;

            postProj.m00 = highwayScale;
            postProj.m11 = highwayScale;
            postProj.m03 = offsetX;
            postProj.m13 = offsetY;

            return postProj;
        }

        /// <summary>
        /// Generates the modified projection matrix (postProj * camProj).
        /// </summary>
        public static Matrix4x4 GetModifiedProjectionMatrix(Matrix4x4 camProj, int index, int highwayCount, float highwayScale)
        {
            Matrix4x4 postProj = GetPostProjectionMatrix(index, highwayCount, highwayScale);
            return postProj * camProj; // HLSL-style: mul(postProj, proj)
        }
    }
}
