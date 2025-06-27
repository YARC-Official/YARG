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

        private List<Camera> _cameras = new();
        private List<Vector3> _highwayPositions = new();

        private Camera _renderCamera;
        private RenderTexture _highwaysOutputTexture;

        private float[] _curveFactors = new float[MAX_MATRICES];
        private float[] _zeroFadePositions = new float[MAX_MATRICES];
        private float[] _fadeSize = new float[MAX_MATRICES];
        private float[] _fadeParams = new float[MAX_MATRICES * 2];
        private Matrix4x4[] _camViewMatrices = new Matrix4x4[MAX_MATRICES];
        private Matrix4x4[] _camInvViewMatrices = new Matrix4x4[MAX_MATRICES];
        private Matrix4x4[] _camProjMatrices = new Matrix4x4[MAX_MATRICES];
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


        private Vector2 WorldToViewport(Vector3 positionWS, int index)
        {
            Vector4 clipSpacePos = (_camProjMatrices[index] * _camViewMatrices[index]) * new Vector4(positionWS.x, positionWS.y, positionWS.z, 1.0f);
            // Perspective divide to get NDC
            float ndcX = clipSpacePos.x / clipSpacePos.w;
            float ndcY = clipSpacePos.y / clipSpacePos.w;

            // NDC [-1, 1] → Viewport [0, 1]
            float viewportX = (ndcX + 1.0f) * 0.5f;
            float viewportY = (ndcY + 1.0f) * 0.5f;

            Vector2 viewportPos = new Vector2(viewportX, viewportY);
            return viewportPos;
        }

        private Vector2 CalculateFadeParams(int index, Vector3 trackPosition, float ZeroFadePosition, float FadeSize)
        {
            var trackZeroFadePosition = new Vector3(trackPosition.x, trackPosition.y, ZeroFadePosition - FadeSize);
            var trackFullFadePosition = new Vector3(trackPosition.x, trackPosition.y, ZeroFadePosition);
            var fadeStart = WorldToViewport(trackZeroFadePosition, index).y;
            var fadeEnd = WorldToViewport(trackFullFadePosition, index).y;
            return new Vector2(fadeStart, fadeEnd);
        }

        private void RecalculateCameraBounds()
        {
            float maxWorld = float.NaN;
            float minWorld = float.NaN;
            foreach (var position in _highwayPositions)
            {
                // This doesn't matter too much as long
                // as everything fits. This is just for frustrum culling.
                var x = position.x;
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

        // This is only directly used for fake track player really
        // Rest should go through AddPlayer
        public void AddPlayerParams(Vector3 position, Camera TrackCamera, float CurveFactor, float ZeroFadePosition, float FadeSize)
        {
            var index = _cameras.Count;
            // This equation calculates a good scale for all of the tracks.
            // It was made with experimentation; there's probably a "real" formula for this.
            _scale = Mathf.Max(0.7f * Mathf.Log10(index), 0f);
            _scale = 1f - _scale;

            _cameras.Add(TrackCamera);
            _highwayPositions.Add(position);

            UpdateCameraProjectionMatrices();
            UpdateCurveFactor(CurveFactor, index);
            UpdateFadeParams(index, ZeroFadePosition, FadeSize);
        }

        public void UpdateCurveFactor(float CurveFactor, int index)
        {
            _curveFactors[index] = CurveFactor;
            Shader.SetGlobalFloatArray(YargCurveFactorsID, _curveFactors);
        }

        private void RecalculateFadeParams()
        {
            for (int index = 0; index < _cameras.Count; ++index)
            {
                var fadeParams = CalculateFadeParams(index, _highwayPositions[index], _zeroFadePositions[index], _fadeSize[index]);
                _fadeParams[index * 2] = fadeParams.x;
                _fadeParams[index * 2 + 1] = fadeParams.y;

            }
            Shader.SetGlobalFloatArray(YargFadeParamsID, _fadeParams);
        }

        public void UpdateFadeParams(int index, float ZeroFadePosition, float FadeSize)
        {
            _fadeSize[index] = FadeSize > 0.0 ? FadeSize : 0.0001f;
            _zeroFadePositions[index] = ZeroFadePosition;
            RecalculateFadeParams();
        }

        public void AddTrackPlayer(TrackPlayer trackPlayer)
        {
            // This effectively disables rendering it but keeps components active
            var cameraData = trackPlayer.TrackCamera.GetUniversalAdditionalCameraData();
            cameraData.renderType = CameraRenderType.Overlay;

            AddPlayerParams(trackPlayer.transform.position, trackPlayer.TrackCamera, trackPlayer.Player.CameraPreset.CurveFactor, trackPlayer.ZeroFadePosition, trackPlayer.FadeSize);
            RecalculateCameraBounds();
        }


        private void Awake()
        {
        }

        private void OnEnable()
        {
            _renderCamera = GetComponent<Camera>();
            if (_highwaysOutput != null)
            {
                _renderCamera.targetTexture = GetHighwayOutputTexture();
            }

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

            if (_cameras.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _cameras.Count; ++i)
            {
                var camera = _cameras[i];
                _camViewMatrices[i] = camera.worldToCameraMatrix;
                _camInvViewMatrices[i] = camera.cameraToWorldMatrix;
            }
            Shader.SetGlobalMatrixArray(YargHighwayCamViewMatricesID, _camViewMatrices);
            Shader.SetGlobalMatrixArray(YargHighwayCamInvViewMatricesID, _camInvViewMatrices);
            Shader.SetGlobalInteger(YargHighwaysNumberID, _cameras.Count);
        }

        private void UpdateCameraProjectionMatrices()
        {
            for (int i = 0; i < _cameras.Count; ++i)
            {
                var camera = _cameras[i];
                _camViewMatrices[i] = camera.worldToCameraMatrix;
                _camInvViewMatrices[i] = camera.cameraToWorldMatrix;
                var projMatrix = GetModifiedProjectionMatrix(camera.projectionMatrix,
                                                             i, _cameras.Count, _scale);
                _camProjMatrices[i] = GL.GetGPUProjectionMatrix(projMatrix, SystemInfo.graphicsUVStartsAtTop /* if we're not rendering to render texture this has to be changed to always false */);
                Shader.SetGlobalMatrixArray(YargHighwayCamProjMatricesID, _camProjMatrices);
            }
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
            float offsetY = -1.0f + highwayScale; // Offset down if scaled vertically

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
