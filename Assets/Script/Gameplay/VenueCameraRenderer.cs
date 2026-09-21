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
        // Trails feedback ping-pong: the venue PP pass samples `_trailsTexture`
        // (global `_YargPrevFrame`) and writes `_trailsTextureAlt`; the pair is
        // swapped after each venue camera render so the pass never samples the
        // texture it renders into.
        private static RenderTexture _trailsTexture;
        private static RTHandle _trailsTextureHandle;
        private static RenderTexture _trailsTextureAlt;
        private static RTHandle _trailsTextureAltHandle;
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
        private Material _venuePPMaterial;

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

        private void Awake()
        {
            _venuePPMaterial = CreateMaterial("Hidden/YARG/VenuePP");
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

            ReleaseTrailsTextures(destroy: false);

            var descriptor = new RenderTextureDescriptor(outputWidth, outputHeight, RenderTextureFormat.DefaultHDR, 16, 0);

            if (_venueOutput != null)
            {
                // Don't actually create the texture unless _venueOutput is set
                _venueTexture = new RenderTexture(descriptor);
                _venueTexture.Create();
                _venueOutput.texture = _venueTexture;
            }
            else
            {
                // We check again because it is possible for venue to load before the rest of the gameplay scene
                var venueOutputObject = GameObject.Find("Venue Output");
                if (venueOutputObject != null)
                {
                    _venueOutput = venueOutputObject.GetComponent<RawImage>();
                }
            }

            descriptor.depthBufferBits = 0;
            _trailsTexture = CreateTrailsTexture(descriptor);
            _trailsTextureHandle = RTHandles.Alloc(_trailsTexture);
            _trailsTextureAlt = CreateTrailsTexture(descriptor);
            _trailsTextureAltHandle = RTHandles.Alloc(_trailsTextureAlt);
            Shader.SetGlobalTexture(_trailsTextureId, _trailsTexture);
        }

        private static RenderTexture CreateTrailsTexture(RenderTextureDescriptor descriptor)
        {
            var texture = new RenderTexture(descriptor);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Create();
            Graphics.Blit(Texture2D.blackTexture, texture);
            return texture;
        }

        private static void ReleaseTrailsTextures(bool destroy)
        {
            ReleaseTrailsTexture(ref _trailsTexture, ref _trailsTextureHandle, destroy);
            ReleaseTrailsTexture(ref _trailsTextureAlt, ref _trailsTextureAltHandle, destroy);
        }

        private static void ReleaseTrailsTexture(ref RenderTexture texture, ref RTHandle handle, bool destroy)
        {
            handle?.Release();
            handle = null;

            if (texture == null)
            {
                return;
            }

            texture.Release();
            if (destroy)
            {
                Destroy(texture);
            }
            else
            {
                texture.DiscardContents();
            }
            texture = null;
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

            ReleaseTrailsTextures(destroy: true);

            if (_venuePPMaterial != null)
            {
                CoreUtils.Destroy(_venuePPMaterial);
                _venuePPMaterial = null;
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

            ReleaseTrailsTextures(destroy: true);

            _venueOutput = null;
        }

        private void Update()
        {
            if (ScreenSizeDetector.HasScreenSizeChanged || _venueTexture == null)
            {
                RecreateTextures();
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
                // Divisor is relative to 60 FPS, so target is always 60/divisor
                _effectiveFps = Mathf.RoundToInt(60f / fpsEffect.Divisor.value);
                // Clamp to FPS cap if non-zero (no cap when FPS=0)
                if (FPS > 0)
                {
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
            Shader.SetGlobalInt(_scanlineSizeId, 0);
            Shader.SetGlobalFloat(_trailsLengthId, 0);

            // Swap the trails ping-pong: the texture just written becomes next
            // frame's `_YargPrevFrame` source.
            (_trailsTexture, _trailsTextureAlt) = (_trailsTextureAlt, _trailsTexture);
            (_trailsTextureHandle, _trailsTextureAltHandle) = (_trailsTextureAltHandle, _trailsTextureHandle);
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam != _renderCamera)
            {
                return;
            }

            // Point the trails feedback sample at the previous frame's texture.
            Shader.SetGlobalTexture(_trailsTextureId, _trailsTexture);

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
                        _venuePPMaterial.EnableKeyword(_mirrorKeywords[i]);
                    }
                    else
                    {
                        _venuePPMaterial.DisableKeyword(_mirrorKeywords[i]);
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

        /// <summary>
        /// Venue post pass: applies VenuePP (mirror wipe, posterize, scanlines, trails
        /// feedback) after URP post-processing, forces alpha to 1.0, and writes the
        /// result into the persistent trails texture so the next frame can sample it
        /// as <c>_YargPrevFrame</c>. The patched UberPost shader is no longer needed
        /// for venue effects.
        /// </summary>
        private sealed class VenuePostPostProcessingPass : ScriptableRenderPass
        {
            private readonly Material _venuePPMaterial;

            public VenuePostPostProcessingPass(VenueCameraRenderer vcr)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                _venuePPMaterial = vcr._venuePPMaterial;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                TextureHandle source = resourceData.activeColorTexture;
                // Write the "alt" trails texture while the shader samples the other
                // one as `_YargPrevFrame`, so no pass ever reads what it writes.
                TextureHandle destination = renderGraph.ImportTexture(_trailsTextureAltHandle);

                var blitParams = new BlitMaterialParameters(source, destination, _venuePPMaterial, 0);
                renderGraph.AddBlitPass(blitParams, passName: "Venue PP");

                // Update cameraColor so the final blit uses the venue-processed, alpha-fixed texture.
                resourceData.cameraColor = destination;
            }
        }

    }
}
