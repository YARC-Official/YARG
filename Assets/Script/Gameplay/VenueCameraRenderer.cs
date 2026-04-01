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

        private int _frameCount;
        private float _elapsedTime;
        private static float _timeSinceLastRender;
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

            if (_trailsTexture != null)
            {
                _trailsTexture.Release();
                _trailsTexture.DiscardContents();
            }

            var descriptor = new RenderTextureDescriptor(outputWidth, outputHeight, RenderTextureFormat.DefaultHDR, 16, 0);
            _venueTexture = new RenderTexture(descriptor);
            _venueTexture.Create();
            _venueOutput.texture = _venueTexture;

            descriptor.depthBufferBits = 0;
            _trailsTexture = new RenderTexture(descriptor);
            _trailsTexture.filterMode = FilterMode.Bilinear;
            _trailsTexture.wrapMode = TextureWrapMode.Clamp;
            _trailsTexture.Create();
            Shader.SetGlobalTexture(_trailsTextureId, _trailsTexture);
            Graphics.Blit(Texture2D.blackTexture, _trailsTexture);
        }

        private void OnEnable()
        {
            FPS = SettingsManager.Settings.VenueFpsCap.Value;
            _timeSinceLastRender = 0f;
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
                _trailsTexture.Release();
                Destroy(_trailsTexture);
                _trailsTexture = null;
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
                _timeSinceLastRender = float.MaxValue;
            }

            var stack = VolumeManager.instance.stack;

            VolumeManager.instance.Update(_renderCamera.gameObject.transform, _venueLayerMask);

            _effectiveFps = FPS;

            var fpsEffect = stack.GetComponent<SlowFPSComponent>();

            if (fpsEffect.IsActive())
            {
                // The divisor is relative to 60 fps, so we need to adjust for that if FPS is something other than 60
                // TODO: Consider using ActualFPS here
                var fpsRatio = FPS / 60f;
                var adjustedDivisor = fpsRatio * fpsEffect.Divisor.value;
                _effectiveFps = Mathf.RoundToInt(FPS / adjustedDivisor);
                // Don't allow a rate higher than the FPS cap
                _effectiveFps = Mathf.Min(FPS, _effectiveFps);
            }

            // Increment wall clock time regardless of whether we render a frame
            _timeSinceLastRender += Time.unscaledDeltaTime;
            _elapsedTime += Time.unscaledDeltaTime;

            float targetInterval = 1f / _effectiveFps;

            if (_timeSinceLastRender >= targetInterval)
            {
                Render();

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
                var adjustedLength = Mathf.Pow(trailsEffect.Length, _effectiveFps / 60f);
                Shader.SetGlobalFloat(_trailsLengthId, adjustedLength);
            }

            var renderer = _renderCamera.GetUniversalAdditionalCameraData().scriptableRenderer;
            renderer.EnqueuePass(_pass);
        }

        private void Render()
        {
            // Create a standard request
            var request = new RenderPipeline.StandardRequest();

            // Check if the request is supported by the active render pipeline
            if (RenderPipeline.SupportsRenderRequest(_renderCamera, request))
            {
                request.destination = _venueTexture;
                // Render camera and fill texture2D with its view
                RenderPipeline.SubmitRenderRequest(_renderCamera, request);

                if (!IsRendered)
                {
                    IsRendered = true;
                }
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
            public VenuePostPostProcessingPass(VenueCameraRenderer vcr)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle trailsTexture = renderGraph.ImportTexture(RTHandles.Alloc(_trailsTexture));
                renderGraph.AddCopyPass(resourceData.activeColorTexture, trailsTexture, "Store frame for trail");
            }
        }

    }
}
