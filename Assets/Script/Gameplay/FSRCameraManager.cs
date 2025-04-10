using FidelityFX;
using FidelityFX.FSR3;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG
{
    public class FSRCameraManager : MonoBehaviour
    {
        // technically this is supported only when SystemInfo.supportsComputeShaders however
        // that seems to be all platforms yarg currently supports anyway
        // This is mostly based on image effect example in
        // fsr3unity repo
        // TODO?:
        // * mipmap bias
        // * reset history on camera cuts
        // * reactive mask
        // * antighosting?
        // * fp16 mode? should improve perf but they also say almost nothing on unity

        [Tooltip("Apply RCAS sharpening to the image after upscaling.")]
        public bool performSharpenPass = true;
        [Tooltip("Strength of the sharpening effect.")]
        [Range(0, 1)] public float sharpness = 0.8f;
        [Tooltip("Adjust the influence of motion vectors on temporal accumulation.")]
        [Range(0, 1)] public float velocityFactor = 1.0f;

        [Header("Exposure")]
        [Tooltip("Allow an exposure value to be computed internally. When set to false, either the provided exposure texture or a default exposure value will be used.")]
        public bool enableAutoExposure = true;
        [Tooltip("Value by which the input signal will be divided, to get back to the original signal produced by the game.")]
        public float preExposure = 1.0f;

        [Header("Debug")]
        [Tooltip("Enable a debug view to analyze the upscaling process.")]
        public bool enableDebugView = false;


        // [Header("Reactivity, Transparency & Composition")] 
        // [Tooltip("Optional texture to control the influence of the current frame on the reconstructed output. If unset, either an auto-generated or a default cleared reactive mask will be used.")]
        // public Texture reactiveMask = null;
        // [Tooltip("Optional texture for marking areas of specialist rendering which should be accounted for during the upscaling process. If unset, a default cleared mask will be used.")]
        // public Texture transparencyAndCompositionMask = null;
        // [Tooltip("Automatically generate a reactive mask based on the difference between opaque-only render output and the final render output including alpha transparencies.")]
        // public bool autoGenerateReactiveMask = true;
        // [Tooltip("Parameters to control the process of auto-generating a reactive mask.")]
        // [SerializeField] private GenerateReactiveParameters generateReactiveParameters = new GenerateReactiveParameters();
        // public GenerateReactiveParameters GenerateReactiveParams => generateReactiveParameters;

        // [System.Serializable]
        // public class GenerateReactiveParameters
        // {
        //     [Tooltip("A value to scale the output")]
        //     [Range(0, 2)] public float scale = 0.5f;
        //     [Tooltip("A threshold value to generate a binary reactive mask")]
        //     [Range(0, 1)] public float cutoffThreshold = 0.2f;
        //     [Tooltip("A value to set for the binary reactive mask")]
        //     [Range(0, 1)] public float binaryValue = 0.9f;
        //     [Tooltip("Flags to determine how to generate the reactive mask")]
        //     public Fsr3Upscaler.GenerateReactiveFlags flags = Fsr3Upscaler.GenerateReactiveFlags.ApplyTonemap | Fsr3Upscaler.GenerateReactiveFlags.ApplyThreshold | Fsr3Upscaler.GenerateReactiveFlags.UseComponentsMax;
        // }


        protected internal RTHandle _output;
        protected internal RTHandle _opaqueOnlyColorBuffer;
        protected internal RTHandle _afterOpaqueOnlyColorBuffer;
        protected internal RTHandle _reactiveMaskOutput;

        private Fsr3UpscalerAssets _assets;
        protected internal Fsr3UpscalerContext _context;

        protected internal readonly Fsr3Upscaler.DispatchDescription _dispatchDescription = new Fsr3Upscaler.DispatchDescription();
        protected internal readonly Fsr3Upscaler.GenerateReactiveDescription _genReactiveDescription = new Fsr3Upscaler.GenerateReactiveDescription();

        public Camera _renderCamera;

        private Vector2Int _displaySize;

        private const GraphicsFormat _graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
        private UniversalRenderPipelineAsset UniversalRenderPipelineAsset;

        private void Awake()
        {
            var descriptor = new RenderTextureDescriptor(
                Screen.width, Screen.height, RenderTextureFormat.ARGBFloat);
            descriptor.mipCount = 0;

            _renderCamera = GetComponent<Camera>();
            _assets = Resources.Load<Fsr3UpscalerAssets>("FSR3 Upscaler Assets");
            _renderCamera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            RenderPipelineManager.endCameraRendering += OnPostCameraRender;


            Fsr3Upscaler.InitializationFlags flags = Fsr3Upscaler.InitializationFlags.EnableMotionVectorsJitterCancellation;

            if (_renderCamera.allowHDR) flags |= Fsr3Upscaler.InitializationFlags.EnableHighDynamicRange;
            if (enableAutoExposure) flags |= Fsr3Upscaler.InitializationFlags.EnableAutoExposure;

            _displaySize = new Vector2Int(_renderCamera.pixelWidth, _renderCamera.pixelHeight);
            _context = Fsr3Upscaler.CreateContext(_displaySize, _displaySize, _assets.shaders, flags);

            UniversalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        }

        private Vector2Int GetScaledRenderSize()
        {
            var scale = UniversalRenderPipelineAsset.renderScale;
            return new Vector2Int((int)(_renderCamera.pixelWidth * scale), (int)(_renderCamera.pixelHeight * scale));
        }

        private void SetupDispatchDescription()
        {
            if (_output != null)
            {
                _output.Release();
            }

            _output = RTHandles.Alloc(_renderCamera.pixelWidth, _renderCamera.pixelHeight, enableRandomWrite: true, colorFormat: _graphicsFormat, msaaSamples: MSAASamples.None, name: "fsr.output");

            // Set up the main FSR3 Upscaler dispatch parameters
            _dispatchDescription.Exposure = ResourceView.Unassigned;
            _dispatchDescription.Reactive = ResourceView.Unassigned;
            _dispatchDescription.TransparencyAndComposition = ResourceView.Unassigned;

            var scaledRenderSize = GetScaledRenderSize();

            _dispatchDescription.Output = new ResourceView(_output);
            _dispatchDescription.PreExposure = preExposure;
            _dispatchDescription.EnableSharpening = performSharpenPass;
            _dispatchDescription.Sharpness = sharpness;
            _dispatchDescription.MotionVectorScale.x = -scaledRenderSize.x;
            _dispatchDescription.MotionVectorScale.y = -scaledRenderSize.y;
            _dispatchDescription.RenderSize = scaledRenderSize;
            _dispatchDescription.UpscaleSize = _displaySize;
            _dispatchDescription.FrameTimeDelta = Time.unscaledDeltaTime;
            _dispatchDescription.CameraNear = _renderCamera.nearClipPlane;
            _dispatchDescription.CameraFar = _renderCamera.farClipPlane;
            _dispatchDescription.CameraFovAngleVertical = _renderCamera.fieldOfView * Mathf.Deg2Rad;
            _dispatchDescription.ViewSpaceToMetersFactor = 1.0f; // 1 unit is 1 meter in Unity
            _dispatchDescription.VelocityFactor = velocityFactor;
            _dispatchDescription.Reset = false;
            _dispatchDescription.Flags = enableDebugView ? Fsr3Upscaler.DispatchFlags.DrawDebugView : 0;;


            if (SystemInfo.usesReversedZBuffer)
            {
                (_dispatchDescription.CameraNear, _dispatchDescription.CameraFar) = (_dispatchDescription.CameraFar, _dispatchDescription.CameraNear);
            }

            // Set up the parameters for the optional experimental auto-TCR feature
            _dispatchDescription.EnableAutoReactive = false;

        }

        private void ApplyJitter()
        {
            var scaledRenderSize = GetScaledRenderSize();
            // Debug.LogFormat("{0} {1}", _displaySize, scaledRenderSize);

            // Perform custom jittering of the camera's projection matrix according to FSR3's recipe
            int jitterPhaseCount = Fsr3Upscaler.GetJitterPhaseCount(scaledRenderSize.x, _displaySize.x);
            Fsr3Upscaler.GetJitterOffset(out float jitterX, out float jitterY, Time.frameCount, jitterPhaseCount);

            _dispatchDescription.JitterOffset = new Vector2(jitterX, jitterY);

            jitterX = 2.0f * jitterX / scaledRenderSize.x;
            jitterY = 2.0f * jitterY / scaledRenderSize.y;

            var jitterTranslationMatrix = Matrix4x4.Translate(new Vector3(jitterX, jitterY, 0));
            var m_projectionMatrix = _renderCamera.projectionMatrix;
            _renderCamera.nonJitteredProjectionMatrix = m_projectionMatrix;
            _renderCamera.projectionMatrix = jitterTranslationMatrix * _renderCamera.nonJitteredProjectionMatrix;
            _renderCamera.useJitteredProjectionMatrixForTransparentRendering = true;
        }

        private RenderTextureFormat GetDefaultFormat()
        {
            return _renderCamera.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        }

        void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            SetupDispatchDescription();
            ApplyJitter();
            var renderer = cam.GetUniversalAdditionalCameraData().scriptableRenderer;
            renderer.EnqueuePass(new FSRPass(this));
            renderer.EnqueuePass(new BlitPass(this));
        }

        void OnPostCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            _renderCamera.ResetProjectionMatrix();
        }


        private void OnDisable()
        {
            DestroyFsrContext();
        }

        private void DestroyFsrContext()
        {

            if (_context != null)
            {
                _context.Destroy();
                _context = null;
            }
        }
    }

    // Render pass to take unscaled rendered picture and FSR it into a render texture
    // This will be done before the final blit (which we'll have to overwrite later)
    public class FSRPass : ScriptableRenderPass
    {
        private FSRCameraManager _fsr;
        private CommandBuffer cmd;

        private readonly int depthTexturePropertyID = Shader.PropertyToID("_CameraDepthTexture");
        private readonly int motionTexturePropertyID = Shader.PropertyToID("_MotionVectorTexture");

        public FSRPass(FSRCameraManager fsr)
        {
            _fsr = fsr;

            // After things are all rendered before final blit
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            cmd = CommandBufferPool.Get("fsr3_execute");

            _fsr._dispatchDescription.Color = new FidelityFX.ResourceView(renderingData.cameraData.renderer.cameraColorTarget, RenderTextureSubElement.Color);
            _fsr._dispatchDescription.Depth = new FidelityFX.ResourceView(Shader.GetGlobalTexture(depthTexturePropertyID), RenderTextureSubElement.Depth);
            _fsr._dispatchDescription.MotionVectors = new FidelityFX.ResourceView(Shader.GetGlobalTexture(motionTexturePropertyID));

            _fsr._context.Dispatch(_fsr._dispatchDescription, cmd);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

    }

    // Render pass to blit final upscaled/antialiased (that was done in FSRPass)
    // picture onto whatever camera is rendering into
    // This is executed after everything is already rendered
    // Note that render pipeline will do its own upscaling and blit and we're
    // overwriting that basically. I don't believe there is a way to remove that builtin blit
    // without using our own render pipeline 
    public class BlitPass : ScriptableRenderPass
    {
        private CommandBuffer cmd;
        private FSRCameraManager _fsr;

        private readonly Vector4 flipVector = new Vector4(1, -1, 0, 1);
        private Vector4 _scaleBias;

        public BlitPass(FSRCameraManager fsr)
        {
            _fsr = fsr;
            renderPassEvent = RenderPassEvent.AfterRendering + 2;
            _scaleBias = SystemInfo.graphicsUVStartsAtTop ? flipVector : Vector4.one;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            cmd = CommandBufferPool.Get("FSR Blit");

            CoreUtils.SetRenderTarget(cmd, BuiltinRenderTextureType.CameraTarget, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, ClearFlag.None, Color.clear);
            cmd.SetViewport(renderingData.cameraData.camera.pixelRect);
            Blitter.BlitTexture(cmd, _fsr._output, _scaleBias, 0, false);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}
