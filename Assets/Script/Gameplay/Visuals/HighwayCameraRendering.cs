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
        [SerializeField]
        private RawImage _highwaysOutput;

        private List<TrackPlayer> _players = new();

        private Camera _renderCamera;
        private RenderTexture _highwaysOutputTexture;

        private Matrix4x4[] _camViewMatrices = null;
        private Matrix4x4[] _camProjMatrices = null;
        // private CurveFadePass _curveFadePass;
        // private ResetParams _resetPass;


        // public static readonly int CurveFactorID = Shader.PropertyToID("_CurveFactor");
        // public static readonly int FadeParamsID = Shader.PropertyToID("_FadeParams");
        // public static readonly int YARGinverseViewAndProjectionMatrix = Shader.PropertyToID("yarg_MatrixInvVP");
        // public static readonly int YARGViewAndProjectionMatrix = Shader.PropertyToID("yarg_MatrixVP");

        // protected internal Vector2 FadeParams;

        public static readonly int YargHighwaysNumberID = Shader.PropertyToID("_YargHighwaysN");
        public static readonly int YargHighwaysScaleID = Shader.PropertyToID("_YargHighwaysScale");
        public static readonly int YargHighwayCamViewMatricesID = Shader.PropertyToID("_YargCamViewMatrices");
        public static readonly int YargHighwayCamProjMatricesID = Shader.PropertyToID("_YargCamProjMatrices");

        public const int MAX_MATRICES = 128;

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
            _renderCamera.transform.position = _renderCamera.transform.position.WithX((minWorld + maxWorld) /  2);
            _renderCamera.orthographicSize = Math.Max(25, (maxWorld - minWorld) / 2);
        }

        public void AddTrackPlayer(TrackPlayer trackPlayer)
        {
            var cameraData = trackPlayer.TrackCamera.GetUniversalAdditionalCameraData();
            // This effectively disables rendering it but keeps components active
            cameraData.renderType = CameraRenderType.Overlay;
            _players.Add(trackPlayer);
            RecalculateCameraBounds();
            _highwaysOutput.texture = _highwaysOutputTexture;

            // This equation calculates a good scale for all of the tracks.
            // It was made with experimentation; there's probably a "real" formula for this.
            float scale = Mathf.Max(0.7f * Mathf.Log10(_players.Count - 1), 0f);
            scale = 1f - scale;
            Shader.SetGlobalFloat(YargHighwaysScaleID, scale);
        }


        private void Awake()
        {
            _renderCamera = GetComponent<Camera>();
            _renderCamera.targetTexture = GetHighwayOutputTexture();
            
            // _curveFadePass = new CurveFadePass(this);
            // _resetPass = new ResetParams();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            // UpdateParams();
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
            Shader.SetGlobalFloat(YargHighwaysNumberID, 0);
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
                _camProjMatrices = new Matrix4x4[128];
            }
            if (_camViewMatrices == null)
            {
                _camViewMatrices = new Matrix4x4[128];
            }

            for (int i = 0; i < _players.Count; ++i)
            {
                var camera = _players[i].TrackCamera;
                _camViewMatrices[i] = camera.worldToCameraMatrix;
                var projMatrix = camera.projectionMatrix;
                _camProjMatrices[i] = GL.GetGPUProjectionMatrix(projMatrix, SystemInfo.graphicsUVStartsAtTop /* if we're not rendering to render texture this has to be changed to always false */);
            }
            Shader.SetGlobalMatrixArray(YargHighwayCamViewMatricesID, _camViewMatrices);
            Shader.SetGlobalMatrixArray(YargHighwayCamProjMatricesID, _camProjMatrices);
            Shader.SetGlobalInteger(YargHighwaysNumberID, _players.Count);


            // var renderer = _renderCamera.GetUniversalAdditionalCameraData().scriptableRenderer;
            // renderer.EnqueuePass(_curveFadePass);
            // renderer.EnqueuePass(_resetPass);
        }

        private void UpdateParams()
        {
            // var worldZeroFadePosition = new Vector3(this.transform.position.x, this.transform.position.y, zeroFadePosition);
            // var worldFullFadePosition = new Vector3(this.transform.position.x, this.transform.position.y, zeroFadePosition - fadeSize);
            // Plane farPlane = new Plane();

            // farPlane.SetNormalAndPosition(_renderCamera.transform.forward, worldZeroFadePosition);
            // var fadeEnd = Mathf.Abs(farPlane.GetDistanceToPoint(_renderCamera.transform.position));

            // farPlane.SetNormalAndPosition(_renderCamera.transform.forward, worldFullFadePosition);
            // var fadeStart = Mathf.Abs(farPlane.GetDistanceToPoint(_renderCamera.transform.position));

            // FadeParams = new Vector2(fadeStart, fadeEnd == fadeStart ? fadeStart + 0.001f : fadeEnd);

            // _prevCurveFactor = CurveFactorID;
            // _prevZeroFade = zeroFadePosition;
            // _prevFadeSize = fadeSize;
        }

        // Curve and Fade could be separate render passes however
        // it seems natural to combine them to not go over
        // whole screen worth of data twice and we do not plan to
        // use them separately from each other
        // private sealed class CurveFadePass : ScriptableRenderPass
        // {
        //     private CommandBuffer _cmd;
        //     private HighwayCameraRendering _highwayCameraRendering;

        //     public CurveFadePass(HighwayCameraRendering highCamRend)
        //     {
        //         _highwayCameraRendering = highCamRend;
        //         renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        //         ConfigureInput(ScriptableRenderPassInput.Depth);
        //     }

        //     public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        //     {
        //         CommandBuffer cmd = CommandBufferPool.Get("HighwayParams");

        //         Matrix4x4 viewMatrix = renderingData.cameraData.GetViewMatrix();
        //         Matrix4x4 gpuProjectionMatrix = renderingData.cameraData.GetGPUProjectionMatrix();
        //         Matrix4x4 viewAndProjectionMatrix = gpuProjectionMatrix * viewMatrix;
        //         Matrix4x4 inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
        //         Matrix4x4 inverseProjectionMatrix = Matrix4x4.Inverse(gpuProjectionMatrix);
        //         Matrix4x4 inverseViewProjection = inverseViewMatrix * inverseProjectionMatrix;
        //         cmd.SetGlobalMatrix(YARGinverseViewAndProjectionMatrix, inverseViewProjection);
        //         cmd.SetGlobalMatrix(YARGViewAndProjectionMatrix, viewAndProjectionMatrix);

        //         cmd.SetGlobalFloat(CurveFactorID, _highwayCameraRendering.curveFactor);
        //         cmd.SetGlobalVector(FadeParamsID, _highwayCameraRendering.FadeParams);

        //         context.ExecuteCommandBuffer(cmd);
        //         cmd.Clear();

        //         CommandBufferPool.Release(cmd);
        //     }
        // }

        // private sealed class ResetParams : ScriptableRenderPass
        // {

        //     public ResetParams()
        //     {
        //         renderPassEvent = RenderPassEvent.AfterRendering;
        //     }

        //     public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        //     {
        //         CommandBuffer cmd = CommandBufferPool.Get("ResetParams");
        //         cmd.SetGlobalFloat(CurveFactorID, 0);
        //         cmd.SetGlobalVector(FadeParamsID, Vector2.zero);
        //         context.ExecuteCommandBuffer(cmd);
        //         cmd.Clear();
        //         CommandBufferPool.Release(cmd);
        //     }
        // }
    }
}
