using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YARG.Core.Logging;
using YARG.Helpers.UI;
using YARG.Settings;
using YARG.Venue.VolumeComponents;

namespace YARG.Gameplay
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class VenueCameraRenderer : MonoBehaviour
    {
        [Range(0.01F, 1.0F)]
        public float renderScale = 1.0F;

        private Camera _renderCamera;
        private float _originalFactor;
        private UniversalRenderPipelineAsset UniversalRenderPipelineAsset;

        private static RawImage _venueOutput;
        private static RenderTexture _venueTexture;
        private static RenderTexture _trailsTexture;

        private static CancellationTokenSource _cts;

        private static Material _trailsMaterial;
        private static Material _scanlineMaterial;
        private static Material _mirrorMaterial;
        private static Material _posterizeMaterial;
        private static Material _alphaClearMaterial;

        private static readonly int _trailsLengthId = Shader.PropertyToID("_Length");
        private static readonly int _posterizeStepsId = Shader.PropertyToID("_YargPosterizeSteps");
        private static readonly int _scanlineIntensityId = Shader.PropertyToID("_YargScanlineIntensity");
        private static readonly int _scanlineSizeId = Shader.PropertyToID("_YargScanlineSize");
        private static readonly int _scanlineColor = Shader.PropertyToID("_YargScanlineColor");
        private static readonly int _scanlineEasingPower = Shader.PropertyToID("_YargScanlineEasingPower");
        private static readonly int _wipeTimeId = Shader.PropertyToID("_YargMirrorWipeTime");
        private static readonly int _startTimeId = Shader.PropertyToID("_YargMirrorStartTime");

        private static readonly string[] _mirrorKeywords = { "YARG_MIRROR_LEFT", "YARG_MIRROR_RIGHT", "YARG_MIRROR_CLOCK_CCW", "YARG_MIRROR_NONE" };

        private VenuePostPostProcessingPass _pass;

        public static float ActualFPS;
        public static float TargetFPS;

        private int _fps;
        private int FPS
        {
            get => _fps;
            set
            {
                _fps = value;
                TargetFPS = value;
            }
        }

        private int _venueLayerMask;

        private bool _didRender;

        private int _frameCount;
        private float _elapsedTime;
        private static float _timeSinceLastRender;

        private static bool _staticsCreated;
        private bool _needsInitialization = true;

        private void Awake()
        {
            _pass = new VenuePostPostProcessingPass(this);

            Shader.SetGlobalColor(_scanlineColor, Color.black);
            Shader.SetGlobalFloat(_scanlineEasingPower, 2.0f);

            renderScale = GraphicsManager.Instance.VenueRenderScale;
            _renderCamera = GetComponent<Camera>();
            // Disable the camera so we can control when it renders
            _renderCamera.enabled = false;

            _renderCamera.allowMSAA = false;
            var cameraData = _renderCamera.GetUniversalAdditionalCameraData();
            cameraData.antialiasing = AntialiasingMode.None;
            switch (GraphicsManager.Instance.VenueAntiAliasing)
            {
                case VenueAntiAliasingMethod.None:
                    break;
                case VenueAntiAliasingMethod.FXAA:
                    cameraData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case VenueAntiAliasingMethod.MSAA:
                    _renderCamera.allowMSAA = true;
                    cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    break;
                case VenueAntiAliasingMethod.TAA:
                    cameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
                    break;
            }
            UniversalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _originalFactor = UniversalRenderPipelineAsset.renderScale;

            FPS = SettingsManager.Settings.VenueFpsCap.Value;
            _venueLayerMask = LayerMask.GetMask("Venue");

            var venueOutputObject = GameObject.Find("Venue Output");
            if (venueOutputObject != null)
            {
                _venueOutput = venueOutputObject.GetComponent<RawImage>();

                if (_venueOutput != null)
                {
                    CreateStatics();
                }
            }
        }

        private void CreateStatics()
        {
            if (_staticsCreated)
            {
                return;
            }

            SceneManager.sceneUnloaded += OnSceneUnloaded;

            _trailsMaterial = CreateMaterial("Trails");
            _scanlineMaterial = CreateMaterial("Scanlines");
            _mirrorMaterial = CreateMaterial("Mirror");
            _posterizeMaterial = CreateMaterial("Posterize");
            _alphaClearMaterial = CreateMaterial("Hidden/AlphaClear");

            _staticsCreated = true;
        }

        private void RecreateTextures()
        {
            if (_venueTexture != null)
            {
                _venueTexture.Release();
                _venueTexture.DiscardContents();
            }

            var outputWidth = (int)(Screen.width * renderScale);
            var outputHeight = (int)(Screen.height * renderScale);

            if (_trailsTexture != null)
            {
                _trailsTexture.Release();
                _trailsTexture.DiscardContents();
            }

            var descriptor = new RenderTextureDescriptor(outputWidth, outputHeight, RenderTextureFormat.DefaultHDR, 16, 0);
            _venueTexture = new RenderTexture(descriptor);
            _venueOutput.texture = _venueTexture;

            descriptor.depthBufferBits = 0;
            _trailsTexture = new RenderTexture(descriptor);
            _trailsTexture.filterMode = FilterMode.Bilinear;
            _trailsTexture.wrapMode = TextureWrapMode.Clamp;
            _trailsTexture.Create();

            Graphics.Blit(Texture2D.blackTexture, _trailsTexture);

        }

        private void OnEnable()
        {
            FPS = SettingsManager.Settings.VenueFpsCap.Value;
            _timeSinceLastRender = 0f;
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnPreCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
        }

        private void OnDestroy()
        {
            if (_venueTexture != null)
            {
                _venueTexture.Release();
                Destroy(_venueTexture);
                _venueTexture = null;
            }

            if (_trailsTexture != null)
            {
                _trailsTexture.Release();
                Destroy(_trailsTexture);
                _trailsTexture = null;
            }

            _venueOutput = null;

            CoreUtils.Destroy(_trailsMaterial);
            CoreUtils.Destroy(_scanlineMaterial);
            CoreUtils.Destroy(_mirrorMaterial);
            CoreUtils.Destroy(_posterizeMaterial);
            CoreUtils.Destroy(_alphaClearMaterial);

            _staticsCreated = false;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (!_staticsCreated)
            {
                return;
            }

            if (_venueTexture != null)
            {
                _venueTexture.Release();
                Destroy(_venueTexture);
                _venueTexture = null;
            }

            if (_trailsTexture != null)
            {
                _trailsTexture.Release();
                Destroy(_trailsTexture);
                _trailsTexture = null;
            }

            _venueOutput = null;

            CoreUtils.Destroy(_trailsMaterial);
            _trailsMaterial = null;
            CoreUtils.Destroy(_scanlineMaterial);
            _scanlineMaterial = null;
            CoreUtils.Destroy(_mirrorMaterial);
            _mirrorMaterial = null;
            CoreUtils.Destroy(_posterizeMaterial);
            _posterizeMaterial = null;
            CoreUtils.Destroy(_alphaClearMaterial);
            _alphaClearMaterial = null;

            _staticsCreated = false;
        }

        private void Update()
        {
            if (ScreenSizeDetector.HasScreenSizeChanged || _needsInitialization)
            {
                RecreateTextures();
                _needsInitialization = false;
                // Force a render this frame to avoid flickering when resizing
                _timeSinceLastRender = float.MaxValue;
            }

            var stack = VolumeManager.instance.stack;

            VolumeManager.instance.Update(_renderCamera.gameObject.transform, _venueLayerMask);

            var effectiveFps = FPS;

            var fpsEffect = stack.GetComponent<SlowFPSComponent>();

            if (fpsEffect.IsActive())
            {
                // The divisor is relative to 60 fps, so we need to adjust for that if FPS is something other than 60
                // TODO: Consider using ActualFPS here
                var fpsRatio = FPS / 60f;
                var adjustedDivisor = fpsRatio * fpsEffect.Divisor.value;
                effectiveFps = Mathf.RoundToInt(FPS / adjustedDivisor);
                // Don't allow a rate higher than the FPS cap
                effectiveFps = Mathf.Min(FPS, effectiveFps);
            }

            // Increment wall clock time regardless of whether we render a frame
            _timeSinceLastRender += Time.unscaledDeltaTime;
            _elapsedTime += Time.unscaledDeltaTime;

            float targetInterval = 1f / effectiveFps;

            if (_timeSinceLastRender >= targetInterval)
            {
                Render(effectiveFps);

                _timeSinceLastRender -= targetInterval;

                // Check to see if we are too far behind..if so, make sure we render next update
                if (_timeSinceLastRender > targetInterval)
                {
                    _timeSinceLastRender = 0f;
                }

                _frameCount++;
            }

            // Update FPS counter
            if (_elapsedTime >= 1f)
            {
                ActualFPS = _frameCount / _elapsedTime;
                _frameCount = 0;
                _elapsedTime = 0f;
            }
        }

        private void OnEndCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }
            Shader.SetGlobalInteger(_posterizeStepsId, 0);
            Shader.SetGlobalFloat(_startTimeId, 0);
            Shader.SetGlobalInt(_scanlineSizeId, 0);
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            var stack = VolumeManager.instance.stack;

            var posterizeEffect = stack.GetComponent<PosterizeComponent>();
            if (posterizeEffect.IsActive())
            {
                Shader.SetGlobalInteger(_posterizeStepsId, posterizeEffect.Steps.value);
            }

            var mirrorEffect = stack.GetComponent<MirrorComponent>();
            if (mirrorEffect.IsActive())
            {
                for(int i = 0; i < _mirrorKeywords.Length; ++i)
                {
                    if (i == mirrorEffect.wipeIndex.value)
                    {
                        Shader.EnableKeyword(_mirrorKeywords[i]);
                    }
                    else
                    {
                        Shader.DisableKeyword(_mirrorKeywords[i]);
                    }
                }
                Shader.SetGlobalFloat(_wipeTimeId, mirrorEffect.wipeTime.value);
                Shader.SetGlobalFloat(_startTimeId, mirrorEffect.startTime.value);
            }

            var scanlineEffect = stack.GetComponent<ScanlineComponent>();
            if (scanlineEffect.IsActive())
            {
                Shader.SetGlobalFloat(_scanlineIntensityId, scanlineEffect.intensity.value);
                Shader.SetGlobalInt(_scanlineSizeId, scanlineEffect.scanlineCount.value);
            }

            var renderer = _renderCamera.GetUniversalAdditionalCameraData().scriptableRenderer;
            renderer.EnqueuePass(_pass);
        }

        private void Render(int effectiveFps)
        {
            _pass.effectiveFPS = effectiveFps;
            // Create a standard request
            var request = new RenderPipeline.StandardRequest();

            // Check if the request is supported by the active render pipeline
            if (RenderPipeline.SupportsRenderRequest(_renderCamera, request))
            {
                request.destination = _venueTexture;
                // Render camera and fill texture2D with its view
                RenderPipeline.SubmitRenderRequest(_renderCamera, request);
            }
        }

        private Material CreateMaterial(string shaderName)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null)
            {
                YargLogger.LogFormatError("Failed to find shader {0}", shaderName);
                return null;
            }

            return CoreUtils.CreateEngineMaterial(shader);
        }

        private sealed class VenuePostPostProcessingPass : ScriptableRenderPass
        {
            private readonly ProfilingSampler _profilingSampler = new ProfilingSampler("VenuePostPostProcessingPass");
            public int effectiveFPS;

            public VenuePostPostProcessingPass(VenueCameraRenderer vcr)
            {
                renderPassEvent = RenderPassEvent.AfterRendering;
                requiresIntermediateTexture = true;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                var destinationDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = 0;

                TextureHandle trailsTexture = renderGraph.ImportTexture(RTHandles.Alloc(_trailsTexture));
                TextureHandle tempTexture = renderGraph.CreateTexture(destinationDesc);

                TextureHandle currentDest = tempTexture;
                TextureHandle currentSource = resourceData.activeColorTexture;


                var stack = VolumeManager.instance.stack;

                Action<string, Material> AddPass = (name, material) =>
                {
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("Scanlines", out var passData, _profilingSampler))
                    {
                        builder.SetInputAttachment(currentSource, 0);
                        builder.SetRenderAttachment(currentDest, 0);
                        passData.material = material;
                        builder.AllowPassCulling(false);
                        builder.SetRenderFunc<PassData>(RenderFunc);

                        (currentSource, currentDest) = (currentDest, currentSource);
                        if (currentDest.Equals(trailsTexture))
                        {
                            if (currentSource.Equals(resourceData.activeColorTexture))
                            {
                                currentDest = tempTexture;
                            }
                            else
                            {
                                currentDest = resourceData.activeColorTexture;
                            }
                        }
                    }
                };

                var trailsEffect = stack.GetComponent<TrailsComponent>();
                if (trailsEffect.IsActive() && _trailsMaterial != null)
                {
                    var adjustedLength = Mathf.Pow(trailsEffect.Length, effectiveFPS / 60f);

                    _trailsMaterial.SetFloat(_trailsLengthId, adjustedLength);

                    currentDest = trailsTexture;
                    AddPass("Trails Pass", _trailsMaterial);
                }

                // var posterizeEffect = stack.GetComponent<PosterizeComponent>();
                // if (posterizeEffect.IsActive() && _posterizeMaterial != null)
                // {
                //     _posterizeMaterial.SetInteger(_posterizeStepsId, posterizeEffect.Steps.value);
                //     AddPass("Posterize Pass", _posterizeMaterial);
                // }

                // var mirrorEffect = stack.GetComponent<MirrorComponent>();
                // if (mirrorEffect.IsActive() && _mirrorMaterial != null)
                // {
                //     _mirrorMaterial.shaderKeywords = Array.Empty<string>();
                //     _mirrorMaterial.EnableKeyword(_mirrorKeywords[mirrorEffect.wipeIndex.value]);
                //     _mirrorMaterial.SetFloat(_wipeTimeId, mirrorEffect.wipeTime.value);
                //     _mirrorMaterial.SetFloat(_startTimeId, mirrorEffect.startTime.value);
                //     AddPass("Mirror Pass", _mirrorMaterial);
                // }

                // var scanlineEffect = stack.GetComponent<ScanlineComponent>();
                // if (scanlineEffect.IsActive() && _scanlineMaterial != null)
                // {
                //     _scanlineMaterial.SetFloat(_scanlineIntensityId, scanlineEffect.intensity.value);
                //     _scanlineMaterial.SetInt(_scanlineSizeId, scanlineEffect.scanlineCount.value);

                //     AddPass("Scanlines", _scanlineMaterial);
                // }

                if (!currentSource.Equals(resourceData.activeColorTexture))
                {
                    renderGraph.AddCopyPass(currentSource, resourceData.activeColorTexture, passName: "Copy Temp Texture to Active Color Texture");
                }
            }

            private static void RenderFunc(PassData data, RasterGraphContext context)
            {
                Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), data.material, 0);
            }

            private class PassData
            {
                public Material material;
            }
        }

    }
}
