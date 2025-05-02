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
        private Shader _Shader;

        private float _prevZeroFade;
        private float _prevFadeSize;
        private float _prevCurveFactor;

        private static readonly int _curveFactor = Shader.PropertyToID("_CurveFactor");
        private static readonly int _FadeParams = Shader.PropertyToID("_FadeParams");

        protected internal Material _Material;

        private void Awake()
        {
            _renderCamera = GetComponent<Camera>();
            _curveFadePass = new CurveFadePass(this);
            _Shader = Shader.Find("HighwayBlit");
        }

        private void OnEnable()
        {
            _Material = CoreUtils.CreateEngineMaterial(_Shader);
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            UpdateParams();
        }

        private void OnDisable()
        {
            CoreUtils.Destroy(_Material);
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

            _Material.SetVector(_FadeParams, new Vector2(fadeStart, fadeEnd));

            _Material.SetFloat(_curveFactor, curveFactor);

            _prevCurveFactor = _curveFactor;
            _prevZeroFade = zeroFadePosition;
            _prevFadeSize = fadeSize;
        }

        // Curve and Fade could be separate render passes however
        // it seems natural to combine them to not go over
        // whole screen worth of data twice and we do not plan to
        // use them separately from each other
        private sealed class CurveFadePass : ScriptableRenderPass
        {
            private static readonly int _MainTex = Shader.PropertyToID("_MainTex");
            private ProfilingSampler _ProfilingSampler = new ProfilingSampler("HighwayBlit");
            private CommandBuffer _cmd;
            private HighwayCameraRendering _highwayCameraRendering;
            MethodInfo SwapColorBuffer = null;

            public CurveFadePass(HighwayCameraRendering highCamRend)
            {
                _highwayCameraRendering = highCamRend;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_highwayCameraRendering._Material == null)
                {
                    return;
                }

                ScriptableRenderer renderer = renderingData.cameraData.renderer;
                CommandBuffer cmd = CommandBufferPool.Get("HighwayBlit");

                if (SwapColorBuffer == null)
                {
                    SwapColorBuffer = renderer.GetType().GetMethod("SwapColorBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                using (new ProfilingScope(cmd, _ProfilingSampler))
                {
                    cmd.SetGlobalTexture(_MainTex, renderer.cameraColorTarget);

                    // Force color buffer swap
                    SwapColorBuffer.Invoke(renderer, new object[] { cmd });
                    cmd.SetRenderTarget(renderer.cameraColorTarget);

                    //The RenderingUtils.fullscreenMesh argument specifies that the mesh to draw is a quad.
                    cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, _highwayCameraRendering._Material);
                }
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                CommandBufferPool.Release(cmd);
            }
        }
    }
}
