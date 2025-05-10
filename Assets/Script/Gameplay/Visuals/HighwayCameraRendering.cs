using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Gameplay.Visuals
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class HighwayCameraRendering : MonoBehaviour
    {
        [Range(-3.0F, 3.0F)]
        public float curveFactor = 0.5F;
        [Range(0.00F, 100.0F)]
        public float zeroFadePosition = 3.0F;
        [Range(0.00F, 5.0F)]
        public float fadeSize = 1.25F;

        private Camera _renderCamera;
        private CurveFadePass _curveFadePass;
        private ResetParams _resetPass;
        private Shader _Shader;

        private float _prevZeroFade;
        private float _prevFadeSize;
        private float _prevCurveFactor;

        public static readonly int CurveFactorID = Shader.PropertyToID("_CurveFactor");
        public static readonly int FadeParamsID = Shader.PropertyToID("_FadeParams");
        public static readonly int YARGinverseViewAndProjectionMatrix = Shader.PropertyToID("yarg_MatrixInvVP");
        public static readonly int YARGViewAndProjectionMatrix = Shader.PropertyToID("yarg_MatrixVP");

        protected internal Vector2 FadeParams;

        private void Awake()
        {
            _renderCamera = GetComponent<Camera>();
            _curveFadePass = new CurveFadePass(this);
            _resetPass = new ResetParams();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            UpdateParams();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnPreCameraRender;
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            if (_prevCurveFactor != curveFactor || _prevZeroFade != zeroFadePosition || _prevFadeSize != fadeSize)
            {
                UpdateParams();
            }

            var renderer = _renderCamera.GetUniversalAdditionalCameraData().scriptableRenderer;
            renderer.EnqueuePass(_curveFadePass);
            renderer.EnqueuePass(_resetPass);
        }

        private void UpdateParams()
        {
            var worldZeroFadePosition = new Vector3(this.transform.position.x, this.transform.position.y, zeroFadePosition);
            var worldFullFadePosition = new Vector3(this.transform.position.x, this.transform.position.y, zeroFadePosition - fadeSize);
            Plane farPlane = new Plane();

            farPlane.SetNormalAndPosition(_renderCamera.transform.forward, worldZeroFadePosition);
            var fadeEnd = Mathf.Abs(farPlane.GetDistanceToPoint(_renderCamera.transform.position));

            farPlane.SetNormalAndPosition(_renderCamera.transform.forward, worldFullFadePosition);
            var fadeStart = Mathf.Abs(farPlane.GetDistanceToPoint(_renderCamera.transform.position));

            FadeParams = new Vector2(fadeStart, fadeEnd == fadeStart ? fadeStart + 0.001f : fadeEnd);

            _prevCurveFactor = CurveFactorID;
            _prevZeroFade = zeroFadePosition;
            _prevFadeSize = fadeSize;
        }

        // Curve and Fade could be separate render passes however
        // it seems natural to combine them to not go over
        // whole screen worth of data twice and we do not plan to
        // use them separately from each other
        private sealed class CurveFadePass : ScriptableRenderPass
        {
            private CommandBuffer _cmd;
            private HighwayCameraRendering _highwayCameraRendering;

            public CurveFadePass(HighwayCameraRendering highCamRend)
            {
                _highwayCameraRendering = highCamRend;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("HighwayParams");

                Matrix4x4 viewMatrix = renderingData.cameraData.GetViewMatrix();
                Matrix4x4 gpuProjectionMatrix = renderingData.cameraData.GetGPUProjectionMatrix();
                Matrix4x4 viewAndProjectionMatrix = gpuProjectionMatrix * viewMatrix;
                Matrix4x4 inverseViewMatrix = Matrix4x4.Inverse(viewMatrix);
                Matrix4x4 inverseProjectionMatrix = Matrix4x4.Inverse(gpuProjectionMatrix);
                Matrix4x4 inverseViewProjection = inverseViewMatrix * inverseProjectionMatrix;
                cmd.SetGlobalMatrix(YARGinverseViewAndProjectionMatrix, inverseViewProjection);
                cmd.SetGlobalMatrix(YARGViewAndProjectionMatrix, viewAndProjectionMatrix);

                cmd.SetGlobalFloat(CurveFactorID, _highwayCameraRendering.curveFactor);
                cmd.SetGlobalVector(FadeParamsID, _highwayCameraRendering.FadeParams);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }
        }

        private sealed class ResetParams : ScriptableRenderPass
        {

            public ResetParams()
            {
                renderPassEvent = RenderPassEvent.AfterRendering;
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get("ResetParams");
                cmd.SetGlobalFloat(CurveFactorID, 0);
                cmd.SetGlobalVector(FadeParamsID, Vector2.zero);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
