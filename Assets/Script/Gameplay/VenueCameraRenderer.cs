using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using static UnityEngine.Rendering.RenderGraphModule.Util.RenderGraphUtils;
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
        private static RTHandle _trailsTextureHandle;

        private static readonly int _IsVenueId = Shader.PropertyToID("_YargIsVenue");
        private static readonly int _trailsLengthId = Shader.PropertyToID("_YargTrailLength");
        private static readonly int _trailsTextureId = Shader.PropertyToID("_YargPrevFrame");
        private static readonly int _posterizeStepsId = Shader.PropertyToID("_YargPosterizeSteps");
        private static readonly int _scanlineIntensityId = Shader.PropertyToID("_YargScanlineIntensity");
        private static readonly int _scanlineSizeId = Shader.PropertyToID("_YargScanlineSize");
        private static readonly int _scanlineColor = Shader.PropertyToID("_YargScanlineColor");
        private static readonly int _scanlineEasingPower = Shader.PropertyToID("_YargScanlineEasingPower");
        private static readonly int _wipeTimeId = Shader.PropertyToID("_YargMirrorWipeLength");
        private static readonly int _startTimeId = Shader.PropertyToID("_YargMirrorStartTime");

        private static readonly string[] _mirrorKeywords = { "YARG_MIRROR_LEFT", "YARG_MIRROR_RIGHT", "YARG_MIRROR_CLOCK_CCW", "YARG_MIRROR_NONE" };

        private VenuePostPostProcessingPass _pass;
        private Material _alphaFixMaterial;

        public static float ActualFPS;
        public static float TargetFPS;
        public static bool IsRendered { get; private set; }

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
        private int _effectiveFps;

        private int _venueLayerMask;

        private static float _frameAccumulator = 0f;
        private static float _fpsWindowStart = 0f;
        private static int _fpsWindowFrames = 0;
        private bool _needsInitialization = true;

        private void Awake()
        {
            _alphaFixMaterial = CreateMaterial("Hidden/YARG/VenueAlphaFix");
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
            }
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

            ScalableBufferManager.ResizeBuffers(renderScale, renderScale);

            if (_trailsTexture != null)
            {
                _trailsTextureHandle?.Release();
                _trailsTextureHandle = null;
                _trailsTexture.Release();
                _trailsTexture.DiscardContents();
            }

            var descriptor = new RenderTextureDescriptor(outputWidth, outputHeight, RenderTextureFormat.DefaultHDR, 16, 0);
            _venueTexture = new RenderTexture(descriptor);
            _venueTexture.Create();
            if (_venueOutput != null)
            {
                _venueOutput.texture = _venueTexture;
            }

            descriptor.depthBufferBits = 0;
            _trailsTexture = new RenderTexture(descriptor);
            _trailsTexture.filterMode = FilterMode.Bilinear;
            _trailsTexture.wrapMode = TextureWrapMode.Clamp;
            _trailsTexture.Create();
            _trailsTextureHandle = RTHandles.Alloc(_trailsTexture);
            Shader.SetGlobalTexture(_trailsTextureId, _trailsTexture);
            Graphics.Blit(Texture2D.blackTexture, _trailsTexture);
        }

        private static void ResetRenderState()
        {
            _frameAccumulator = 0f;
            _fpsWindowStart = 0f;
            _fpsWindowFrames = 0;
        }

        private void OnEnable()
        {
            FPS = SettingsManager.Settings.VenueFpsCap.Value;
            ResetRenderState();
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
            RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnPreCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
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
                _trailsTextureHandle?.Release();
                _trailsTextureHandle = null;
                _trailsTexture.Release();
                Destroy(_trailsTexture);
                _trailsTexture = null;
            }

            if (_alphaFixMaterial != null)
            {
                CoreUtils.Destroy(_alphaFixMaterial);
                _alphaFixMaterial = null;
            }

            _venueOutput = null;
            IsRendered = false;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (_venueTexture != null)
            {
                _venueTexture.Release();
                Destroy(_venueTexture);
                _venueTexture = null;
            }

            if (_trailsTexture != null)
            {
                _trailsTextureHandle?.Release();
                _trailsTextureHandle = null;
                _trailsTexture.Release();
                Destroy(_trailsTexture);
                _trailsTexture = null;
            }

            _venueOutput = null;
        }

        private void Update()
        {
            if (ScreenSizeDetector.HasScreenSizeChanged || _needsInitialization)
            {
                RecreateTextures();
                _needsInitialization = false;
                // Force a render this frame to avoid flickering when resizing
                ResetRenderState();
            }

            // Update the global volume stack with venue effects so SlowFPS
            // (and any other effects read in Update()) can access them.
            VolumeManager.instance.Update(_renderCamera.gameObject.transform, _venueLayerMask);

            _effectiveFps = FPS;

            var stack = VolumeManager.instance.stack;
            var fpsEffect = stack.GetComponent<SlowFPSComponent>();

            if (fpsEffect.IsActive())
            {
                if (FPS == 0)
                {
                    _effectiveFps = Mathf.RoundToInt(60f / fpsEffect.Divisor.value);
                } else {
                    // The divisor is relative to 60 fps, so we need to adjust for that if FPS is something other than 60
                    var fpsRatio = ActualFPS / 60f;
                    var adjustedDivisor = fpsRatio * fpsEffect.Divisor.value;
                    _effectiveFps = Mathf.RoundToInt(FPS / adjustedDivisor);
                    // Don't allow a rate higher than the FPS cap
                    _effectiveFps = Mathf.Min(FPS, _effectiveFps);
                }
            }

            // Increment wall clock time regardless of whether we render a frame
            var currentFrameTime = Time.unscaledTime;

            // Accumulator-based FPS limiting: smooths quantization over time.
            // Add dt each frame, when accumulator >= frameInterval, render and subtract.
            // This averages to the exact target FPS regardless of Update() frequency.
            float frameInterval = _effectiveFps > 0 ? 1f / _effectiveFps : 0f;
            _frameAccumulator += Time.unscaledDeltaTime;

            if (_effectiveFps == 0 || _frameAccumulator >= frameInterval)
            {
                // Sliding window: reset every ~1 second, compute FPS from frame count / elapsed time.
                if (_fpsWindowStart > 0f && currentFrameTime - _fpsWindowStart > 1.0f)
                {
                    ActualFPS = _fpsWindowFrames / (currentFrameTime - _fpsWindowStart);
                    _fpsWindowStart = currentFrameTime;
                    _fpsWindowFrames = 0;
                }

                _fpsWindowFrames++;
                if (_fpsWindowFrames == 1)
                {
                    _fpsWindowStart = currentFrameTime;
                }

                Render();
                _frameAccumulator -= frameInterval;
            }
        }

        private void OnEndCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            // Disable the camera after rendering so it only renders when explicitly triggered
            _renderCamera.enabled = false;
            _renderCamera.targetTexture = null;

            Shader.SetGlobalInteger(_posterizeStepsId, 0);
            Shader.SetGlobalFloat(_startTimeId, 0);
            Shader.SetGlobalFloat(_IsVenueId, 0);
            Shader.SetGlobalInt(_scanlineSizeId, 0);
            Shader.SetGlobalFloat(_trailsLengthId, 0);
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            Shader.SetGlobalFloat(_IsVenueId, 1);

            // URP replaces VolumeManager.instance.stack with either the global stack
            // or the camera's local volumeStack during rendering setup, depending on
            // the volume framework update mode. We need to update the same stack that
            // URP is using, so we update it here (after URP's setup) before reading.
            VolumeManager.instance.Update(VolumeManager.instance.stack, _renderCamera.gameObject.transform, _venueLayerMask);

            var stack = VolumeManager.instance.stack;

            var posterizeEffect = stack.GetComponent<PosterizeComponent>();
            if (posterizeEffect.IsActive())
            {
                YargLogger.LogFormatTrace("Venue PP: posterize, steps: {0}", posterizeEffect.Steps.value);
                Shader.SetGlobalInteger(_posterizeStepsId, posterizeEffect.Steps.value);
            }

            var mirrorEffect = stack.GetComponent<MirrorComponent>();
            if (mirrorEffect.IsActive())
            {
                for (int i = 0; i < _mirrorKeywords.Length; ++i)
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
                YargLogger.LogFormatTrace("Venue PP: mirror, wipeStart: {0}", mirrorEffect.startTime.value);
                Shader.SetGlobalFloat(_wipeTimeId, mirrorEffect.wipeTime.value);
                Shader.SetGlobalFloat(_startTimeId, mirrorEffect.startTime.value);
            }

            var scanlineEffect = stack.GetComponent<ScanlineComponent>();
            if (scanlineEffect.IsActive())
            {
                YargLogger.LogFormatTrace("Venue PP: scanline, line count: {0}", scanlineEffect.scanlineCount.value);
                Shader.SetGlobalFloat(_scanlineIntensityId, scanlineEffect.intensity.value);
                Shader.SetGlobalInt(_scanlineSizeId, scanlineEffect.scanlineCount.value);
            }

            var trailsEffect = stack.GetComponent<TrailsComponent>();
            if (trailsEffect.IsActive() )
            {
                YargLogger.LogFormatTrace("Venue PP: trails, length: {0}", trailsEffect.length.value);
                var adjustedLength = Mathf.Pow(trailsEffect.Length, ActualFPS / 60f);
                Shader.SetGlobalFloat(_trailsLengthId, adjustedLength);
            }

            var renderer = _renderCamera.GetUniversalAdditionalCameraData().scriptableRenderer;
            renderer.EnqueuePass(_pass);
        }

        private void Render()
        {
            // Set target texture and enable the camera so it renders through the normal pipeline
            _renderCamera.targetTexture = _venueTexture;
            _renderCamera.enabled = true;

            if (!IsRendered)
            {
                IsRendered = true;
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
            private readonly Material _alphaFixMaterial;

            public VenuePostPostProcessingPass(VenueCameraRenderer vcr)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                _alphaFixMaterial = vcr._alphaFixMaterial;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                TextureHandle source = resourceData.activeColorTexture;
                TextureHandle trailsTexture = renderGraph.ImportTexture(_trailsTextureHandle);

                // Blit through alpha-fix shader to force alpha to 1.0, preventing transparency artifacts
                // when the venue renders without post-processing (UberPP doesn't run to fix alpha).

                var blitParams = new BlitMaterialParameters(source, trailsTexture, _alphaFixMaterial, 0);
                renderGraph.AddBlitPass(blitParams, passName: "Venue Alpha Fix / Trails Copy");

                // Update cameraColor so the final blit uses the alpha-fixed texture.
                resourceData.cameraColor = trailsTexture;
            }
        }

    }
}
