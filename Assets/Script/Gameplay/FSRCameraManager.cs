using System;
using System.Linq;
using FidelityFX;
using FidelityFX.FSR3;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace YARG.Gameplay
{
    public class FSRCameraManager : MonoBehaviour
    {
        // technically this is supported only when SystemInfo.supportsComputeShaders however
        // that seems to be all platforms yarg currently supports anyway
        // This is mostly based on image effect example in
        // fsr3unity repo + reading unity's URP code to understand how default passes work
        // TODO?:
        // * mipmap bias
        // * reset history on camera cuts
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


        [Header("Reactivity, Transparency & Composition")]
        [Tooltip("Automatically generate a reactive mask based on the difference between opaque-only render output and the final render output including alpha transparencies.")]
        public bool autoGenerateReactiveMask = true;
        [Tooltip("Parameters to control the process of auto-generating a reactive mask.")]
        [SerializeField] private GenerateReactiveParameters generateReactiveParameters = new GenerateReactiveParameters();
        public GenerateReactiveParameters GenerateReactiveParams => generateReactiveParameters;

        [System.Serializable]
        public class GenerateReactiveParameters
        {
            [Tooltip("A value to scale the output")]
            [Range(0, 2)] public float scale = 0.5f;
            [Tooltip("A threshold value to generate a binary reactive mask")]
            [Range(0, 1)] public float cutoffThreshold = 0.2f;
            [Tooltip("A value to set for the binary reactive mask")]
            [Range(0, 1)] public float binaryValue = 0.9f;
            [Tooltip("Flags to determine how to generate the reactive mask")]
            public Fsr3Upscaler.GenerateReactiveFlags flags = Fsr3Upscaler.GenerateReactiveFlags.ApplyTonemap | Fsr3Upscaler.GenerateReactiveFlags.ApplyThreshold | Fsr3Upscaler.GenerateReactiveFlags.UseComponentsMax;
        }


        protected internal RTHandle _output;
        protected internal RTHandle _opaqueOnlyColorBuffer;
        protected internal RTHandle _afterOpaqueOnlyColorBuffer;
        protected internal RTHandle _reactiveMaskOutput;

        private Fsr3UpscalerAssets _assets;
        protected internal Fsr3UpscalerContext _context;

        protected internal readonly Fsr3Upscaler.DispatchDescription _dispatchDescription = new Fsr3Upscaler.DispatchDescription();
        protected internal readonly Fsr3Upscaler.GenerateReactiveDescription _genReactiveDescription = new Fsr3Upscaler.GenerateReactiveDescription();

        public Camera renderCamera;
        public GameObject textureParentObject;

        private Vector2Int _displaySize;
        private float _mipmapBiasOffset = 0f;
        protected internal Matrix4x4 _jitterTranslationMatrix;

        // Passes
        private FSRPass _fsrPass;
        private JitterProjectionMatrixPass _jitterOpaquesPass;
        private RestoreProjectionMatrixPass _unJitterOpaquesPass;
        private JitterProjectionMatrixPass _jitterTransparentsPass;
        private RestoreProjectionMatrixPass _unJitterTransparentsPass;
        private CopyColorOpaquePass _copyColorOpaquePass;
        private CopyColorTransparentsPass _copyColorTransparentsPass;

        // Saved renderscale to re-init if it changes
        private float _renderScale;

        private const GraphicsFormat _graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
        private UniversalRenderPipelineAsset UniversalRenderPipelineAsset;

        private void Awake()
        {
            UniversalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _renderScale = UniversalRenderPipelineAsset.renderScale;

            renderCamera = GetComponent<Camera>();
            _assets = Resources.Load<Fsr3UpscalerAssets>("FSR3 Upscaler Assets");
            renderCamera.depthTextureMode = DepthTextureMode.Depth | DepthTextureMode.MotionVectors;
            renderCamera.clearFlags |= CameraClearFlags.Depth;
            renderCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;

            _displaySize = new Vector2Int(renderCamera.pixelWidth, renderCamera.pixelHeight);

            _fsrPass = new FSRPass(this);
            _jitterOpaquesPass = new JitterProjectionMatrixPass(this, RenderPassEvent.BeforeRenderingOpaques);
            _unJitterOpaquesPass = new RestoreProjectionMatrixPass(RenderPassEvent.AfterRenderingOpaques - 1);
            _jitterTransparentsPass = new JitterProjectionMatrixPass(this, RenderPassEvent.BeforeRenderingTransparents);
            _unJitterTransparentsPass = new RestoreProjectionMatrixPass(RenderPassEvent.AfterRenderingTransparents - 1);
            _copyColorOpaquePass = new CopyColorOpaquePass(this);
            _copyColorTransparentsPass = new CopyColorTransparentsPass(this);
        }

        private void CreateFSRContext()
        {
            if (_context != null)
            {
                DestroyFsrContext();
            }
            Fsr3Upscaler.InitializationFlags flags = 0;

            if (renderCamera.allowHDR) flags |= Fsr3Upscaler.InitializationFlags.EnableHighDynamicRange;
            if (enableAutoExposure) flags |= Fsr3Upscaler.InitializationFlags.EnableAutoExposure;

            _context = Fsr3Upscaler.CreateContext(_displaySize, GetScaledRenderSize(), _assets.shaders, flags);
        }

        private Vector2Int GetScaledRenderSize()
        {
            return new Vector2Int((int) (renderCamera.pixelWidth * _renderScale), (int) (renderCamera.pixelHeight * _renderScale));
        }

        private void SetupAutoReactiveDescription()
        {
            // Set up the parameters to auto-generate a reactive mask
            _genReactiveDescription.RenderSize = GetScaledRenderSize();
            _genReactiveDescription.Scale = generateReactiveParameters.scale;
            _genReactiveDescription.CutoffThreshold = generateReactiveParameters.cutoffThreshold;
            _genReactiveDescription.BinaryValue = generateReactiveParameters.binaryValue;
            _genReactiveDescription.Flags = generateReactiveParameters.flags;

            if (_opaqueOnlyColorBuffer != null)
            {
                _opaqueOnlyColorBuffer.Release();
                _opaqueOnlyColorBuffer = null;
            }
            _opaqueOnlyColorBuffer = RTHandles.Alloc(_genReactiveDescription.RenderSize.x, _genReactiveDescription.RenderSize.y, enableRandomWrite: false, colorFormat: _graphicsFormat, msaaSamples: MSAASamples.None, name: "fsr.opaque.only");
            if (_afterOpaqueOnlyColorBuffer != null)
            {
                _afterOpaqueOnlyColorBuffer.Release();
                _afterOpaqueOnlyColorBuffer = null;
            }
            _afterOpaqueOnlyColorBuffer = RTHandles.Alloc(_genReactiveDescription.RenderSize.x, _genReactiveDescription.RenderSize.y, enableRandomWrite: false, colorFormat: _graphicsFormat, msaaSamples: MSAASamples.None, name: "fsr.after.opaque");
            if (_reactiveMaskOutput != null)
            {
                _reactiveMaskOutput.Release();
                _reactiveMaskOutput = null;
            }
            _reactiveMaskOutput = RTHandles.Alloc(_genReactiveDescription.RenderSize.x, _genReactiveDescription.RenderSize.y, enableRandomWrite: true, colorFormat: _graphicsFormat, msaaSamples: MSAASamples.None, name: "fsr.reactivemask");
        }

        private void SetupDispatchDescription()
        {
            if (_output != null)
            {
                _output.Release();
                _output = null;
            }
            _output = RTHandles.Alloc(renderCamera.pixelWidth, renderCamera.pixelHeight, enableRandomWrite: true, colorFormat: _graphicsFormat, msaaSamples: MSAASamples.None, name: "fsr.output");

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
            _dispatchDescription.CameraNear = renderCamera.nearClipPlane;
            _dispatchDescription.CameraFar = renderCamera.farClipPlane;
            _dispatchDescription.CameraFovAngleVertical = renderCamera.fieldOfView * Mathf.Deg2Rad;
            _dispatchDescription.ViewSpaceToMetersFactor = 1.0f; // 1 unit is 1 meter in Unity
            _dispatchDescription.VelocityFactor = velocityFactor;
            _dispatchDescription.Reset = false;
            _dispatchDescription.Flags = enableDebugView ? Fsr3Upscaler.DispatchFlags.DrawDebugView : 0;

            if (SystemInfo.usesReversedZBuffer)
            {
                (_dispatchDescription.CameraNear, _dispatchDescription.CameraFar) = (_dispatchDescription.CameraFar, _dispatchDescription.CameraNear);
            }

            // Set up the parameters for the optional experimental auto-TCR feature
            _dispatchDescription.EnableAutoReactive = false;
        }

        private void ApplyMipmapBias(float biasOffset)
        {
            // Apply a mipmap bias so that textures retain their sharpness
            if (!float.IsNaN(biasOffset) && !float.IsInfinity(biasOffset))
            {
                if (textureParentObject != null)
                {
                    foreach (var tex in textureParentObject.GetComponentsInChildren<Renderer>(true).SelectMany(r =>
                        r.sharedMaterial.GetTexturePropertyNameIDs().Select(name => r.sharedMaterial.GetTexture(name))
                    ).Distinct())
                    {
                        if (tex != null)
                        {
                            tex.mipMapBias += biasOffset;
                        }
                    }
                }
            }
        }

        private void ApplyMipmapBias()
        {
            _mipmapBiasOffset = Fsr3Upscaler.GetMipmapBiasOffset(GetScaledRenderSize().x, _displaySize.x);
            ApplyMipmapBias(_mipmapBiasOffset);
        }

        private void UndoMipmapBias()
        {
            ApplyMipmapBias(-_mipmapBiasOffset);
        }

        private void ApplyJitter()
        {

            var scaledRenderSize = GetScaledRenderSize();

            // Perform custom jittering of the camera's projection matrix according to FSR3's recipe
            int jitterPhaseCount = Fsr3Upscaler.GetJitterPhaseCount(scaledRenderSize.x, _displaySize.x);
            Fsr3Upscaler.GetJitterOffset(out float jitterX, out float jitterY, Time.frameCount, jitterPhaseCount);

            _dispatchDescription.JitterOffset = new Vector2(jitterX, jitterY);

            jitterX = 2.0f * jitterX / scaledRenderSize.x;
            jitterY = -2.0f * jitterY / scaledRenderSize.y;

            _jitterTranslationMatrix = Matrix4x4.Translate(new Vector3(jitterX, jitterY, 0));
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != renderCamera)
            {
                return;
            }
            if (_renderScale != UniversalRenderPipelineAsset.renderScale)
            {
                _renderScale = UniversalRenderPipelineAsset.renderScale;
                OnDisable();
                OnEnable();
            }
            SetupDispatchDescription();
            ApplyJitter();
            var renderer = cam.GetUniversalAdditionalCameraData().scriptableRenderer;
            renderer.EnqueuePass(_jitterOpaquesPass);
            renderer.EnqueuePass(_unJitterOpaquesPass);
            renderer.EnqueuePass(_jitterTransparentsPass);
            renderer.EnqueuePass(_unJitterTransparentsPass);
            renderer.EnqueuePass(_fsrPass);
            // renderer.EnqueuePass(_blitPass);
            if (autoGenerateReactiveMask)
            {
                SetupAutoReactiveDescription();
                renderer.EnqueuePass(_copyColorOpaquePass);
                renderer.EnqueuePass(_copyColorTransparentsPass);
            }
        }

        private void OnDisable()
        {
            DestroyFsrContext();
            if (_output != null)
            {
                _output.Release();
                _output = null;
            }
            if (_opaqueOnlyColorBuffer != null)
            {
                _opaqueOnlyColorBuffer.Release();
                _opaqueOnlyColorBuffer = null;
            }
            if (_afterOpaqueOnlyColorBuffer != null)
            {
                _afterOpaqueOnlyColorBuffer.Release();
                _afterOpaqueOnlyColorBuffer = null;
            }
            if (_reactiveMaskOutput != null)
            {
                _reactiveMaskOutput.Release();
                _reactiveMaskOutput = null;
            }
            RenderPipelineManager.beginCameraRendering -= OnPreCameraRender;
            UndoMipmapBias();
        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            CreateFSRContext();
            ApplyMipmapBias();
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

    // Render pass to apply camera projection matrix jitter
    class JitterProjectionMatrixPass : ScriptableRenderPass
    {
        private FSRCameraManager _fsr;
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("JitterProjectionMatrix");

        public JitterProjectionMatrixPass(FSRCameraManager fsr, RenderPassEvent evt)
        {
            _fsr = fsr;
            renderPassEvent = evt;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddUnsafePass<PassData>("JitterProjectionMatrix", out var passData, _profilingSampler))
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                passData.fsr = _fsr;
                passData.viewMatrix = cameraData.GetViewMatrix();
                passData.projMatrix = cameraData.GetGPUProjectionMatrix();

                builder.SetRenderFunc<PassData>((PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferPool.Get("JitterProjectionMatrix");
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, data.viewMatrix, data.fsr._jitterTranslationMatrix * data.projMatrix, false);
                    Graphics.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                });
            }
        }

        private class PassData
        {
            public FSRCameraManager fsr;
            public Matrix4x4 viewMatrix;
            public Matrix4x4 projMatrix;
        }
    }

    // Render pass to restore camera projection matrix
    class RestoreProjectionMatrixPass : ScriptableRenderPass
    {
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("RestoreProjectionMatrix");

        public RestoreProjectionMatrixPass(RenderPassEvent evt)
        {
            renderPassEvent = evt;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddUnsafePass<PassData>("RestoreProjectionMatrix", out var passData, _profilingSampler))
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                passData.viewMatrix = cameraData.GetViewMatrix();
                passData.projMatrix = cameraData.GetGPUProjectionMatrix();

                builder.SetRenderFunc<PassData>((PassData data, UnsafeGraphContext context) =>
                {
                    var cmd = CommandBufferPool.Get("RestoreProjectionMatrix");
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, data.viewMatrix, data.projMatrix, false);
                    Graphics.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                });
            }
        }

        private class PassData
        {
            public Matrix4x4 viewMatrix;
            public Matrix4x4 projMatrix;
        }
    }

    // Render pass to take unscaled rendered picture and FSR it into a render texture
    class FSRPass : ScriptableRenderPass
    {
        private FSRCameraManager _fsr;
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("FSR3Execute");

        public FSRPass(FSRCameraManager fsr)
        {
            _fsr = fsr;
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var destination = renderGraph.ImportTexture(_fsr._output);
            using (var builder = renderGraph.AddComputePass<PassData>("FSR3Execute", out var passData, _profilingSampler))
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                passData.fsr = _fsr;
                passData.cameraColorHandle = resourceData.activeColorTexture;

                builder.UseTexture(passData.cameraColorHandle, AccessFlags.Read);
                builder.UseTexture(resourceData.motionVectorColor, AccessFlags.Read);
                builder.UseTexture(resourceData.motionVectorDepth, AccessFlags.Read);
                builder.UseTexture(renderGraph.ImportTexture(_fsr._opaqueOnlyColorBuffer), AccessFlags.Read);
                builder.UseTexture(renderGraph.ImportTexture(_fsr._afterOpaqueOnlyColorBuffer), AccessFlags.Read);
                builder.UseTexture(destination, AccessFlags.Write);
                builder.UseTexture(renderGraph.ImportTexture(_fsr._reactiveMaskOutput), AccessFlags.ReadWrite);

                builder.SetRenderFunc<PassData>((PassData data, ComputeGraphContext context) =>
                {
                    var cmd = CommandBufferPool.Get("fsr3_execute");

                    data.fsr._dispatchDescription.Color = new FidelityFX.ResourceView(data.cameraColorHandle, RenderTextureSubElement.Color);
                    data.fsr._dispatchDescription.Depth = new FidelityFX.ResourceView(Shader.GetGlobalTexture("_MotionVectorTexture"), RenderTextureSubElement.Depth);
                    data.fsr._dispatchDescription.MotionVectors = new FidelityFX.ResourceView(Shader.GetGlobalTexture("_MotionVectorTexture"));

                    if (data.fsr.autoGenerateReactiveMask)
                    {
                        data.fsr._genReactiveDescription.ColorOpaqueOnly = new ResourceView(data.fsr._opaqueOnlyColorBuffer);
                        data.fsr._genReactiveDescription.ColorPreUpscale = new ResourceView(data.fsr._afterOpaqueOnlyColorBuffer);
                        data.fsr._genReactiveDescription.OutReactive = new ResourceView(data.fsr._reactiveMaskOutput);
                        data.fsr._context.GenerateReactiveMask(data.fsr._genReactiveDescription, cmd);
                        data.fsr._dispatchDescription.Reactive = new ResourceView(data.fsr._reactiveMaskOutput);
                    }

                    data.fsr._context.Dispatch(data.fsr._dispatchDescription, cmd);

                    Graphics.ExecuteCommandBuffer(cmd);
                    CommandBufferPool.Release(cmd);
                });
            }
            frameData.Get<UniversalResourceData>().cameraColor = destination;
        }

        private class PassData
        {
            public FSRCameraManager fsr;
            public TextureHandle cameraColorHandle;
        }
    }

    // Pass to store copy of color buffer after rendering only opaques
    class CopyColorOpaquePass : ScriptableRenderPass
    {
        private FSRCameraManager _fsr;
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("FSRCopyColorOpaque");

        public CopyColorOpaquePass(FSRCameraManager fsr)
        {
            _fsr = fsr;
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            using (var builder = renderGraph.AddCopyPass(resourceData.activeColorTexture, renderGraph.ImportTexture(_fsr._opaqueOnlyColorBuffer), passName: "Fsr copy opaque"))
            {
            }
        }
    }

    // Pass to store copy of color buffer after rendering transparents
    class CopyColorTransparentsPass : ScriptableRenderPass
    {
        private FSRCameraManager _fsr;
        private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("FSRCopyColorTrans");

        public CopyColorTransparentsPass(FSRCameraManager fsr)
        {
            _fsr = fsr;
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            using (var builder = renderGraph.AddCopyPass(resourceData.activeColorTexture, renderGraph.ImportTexture(_fsr._afterOpaqueOnlyColorBuffer), passName: "Fsr copy transparent"))
            {
            }
        }

    }
}
