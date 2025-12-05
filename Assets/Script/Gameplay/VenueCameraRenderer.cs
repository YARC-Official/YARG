using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using YARG.Core.Logging;
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

        private static RawImage                _venueOutput;
        private static RenderTexture           _venueTexture;
        private static RenderTexture           _trailsTexture;
        private static CancellationTokenSource _cts;

        private static Material _trailsMaterial;
        private static Material _scanlineMaterial;
        private static Material _mirrorMaterial;
        private static Material _posterizeMaterial;
        private static Material _alphaClearMaterial;

        private static readonly int _trailsLengthId = Shader.PropertyToID("_Length");
        private static readonly int _posterizeStepsId = Shader.PropertyToID("_Steps");
        private static readonly int _scanlineIntensityId = Shader.PropertyToID("_ScanlineIntensity");
        private static readonly int _scanlineSizeId = Shader.PropertyToID("_ScanlineSize");
        private static readonly int _wipeTimeId = Shader.PropertyToID("_WipeTime");
        private static readonly int _startTimeId = Shader.PropertyToID("_StartTime");

        private static readonly string[] _mirrorKeywords = { "LEFT", "RIGHT", "CLOCK_CCW", "NONE" };

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

        private int   _frameCount;
        private float _elapsedTime;
        private static float _timeSinceLastRender;

        private static bool _staticsCreated;

        private void Awake()
        {
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
                case VenueAntiAliasingMethod.FSR3:
                    _renderCamera.gameObject.AddComponent<FSRCameraManager>();
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

            var outputWidth = (int)(Screen.width * renderScale);
            var outputHeight = (int)(Screen.height * renderScale);
            _venueTexture = new RenderTexture(outputWidth, outputHeight, 0, RenderTextureFormat.DefaultHDR);
            _venueOutput.texture = _venueTexture;

            _trailsTexture = new RenderTexture(_venueTexture);
            _trailsTexture.filterMode = FilterMode.Bilinear;
            _trailsTexture.wrapMode = TextureWrapMode.Clamp;
            _trailsTexture.Create();

            Graphics.Blit(Texture2D.blackTexture, _trailsTexture);

            _trailsMaterial = CreateMaterial("Trails");
            _scanlineMaterial = CreateMaterial("Scanlines");
            _mirrorMaterial = CreateMaterial("Mirror");
            _posterizeMaterial = CreateMaterial("Posterize");
            _alphaClearMaterial = CreateMaterial("Hidden/AlphaClear");

            _staticsCreated = true;
        }

        private void OnEnable()
        {
            FPS = SettingsManager.Settings.VenueFpsCap.Value;
            _timeSinceLastRender = 0f;
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

        private void Render(int effectiveFps)
        {
            var stack = VolumeManager.instance.stack;

            var descriptor = new RenderTextureDescriptor(_venueTexture.width, _venueTexture.height, _venueTexture.format);
            var rt1 = RenderTexture.GetTemporary(descriptor);
            var rt2 = RenderTexture.GetTemporary(descriptor);

            _renderCamera.targetTexture = rt1;
            _renderCamera.Render();

            RenderTargetIdentifier currentSource = rt1;
            RenderTargetIdentifier currentDest = rt2;

            var cmd = CommandBufferPool.Get("Venue Post Process");

            var trailsEffect = stack.GetComponent<TrailsComponent>();
            if (trailsEffect.IsActive() && _trailsMaterial != null)
            {
                var adjustedLength = Mathf.Pow(trailsEffect.Length, effectiveFps / 60f);

                _trailsMaterial.SetFloat(_trailsLengthId, adjustedLength);
                cmd.Blit(currentSource, _trailsTexture, _trailsMaterial);
                currentSource = _trailsTexture;
            }

            var posterizeEffect = stack.GetComponent<PosterizeComponent>();
            if (posterizeEffect.IsActive() && _posterizeMaterial != null)
            {
                _posterizeMaterial.SetInteger(_posterizeStepsId, posterizeEffect.Steps.value);
                cmd.Blit(currentSource, currentDest, _posterizeMaterial);
                (currentSource, currentDest) = (currentDest, currentSource);
            }

            var mirrorEffect = stack.GetComponent<MirrorComponent>();
            if (mirrorEffect.IsActive() && _mirrorMaterial != null)
            {
                _mirrorMaterial.EnableKeyword(_mirrorKeywords[mirrorEffect.wipeIndex.value]);
                _mirrorMaterial.SetFloat(_wipeTimeId, mirrorEffect.wipeTime.value);
                _mirrorMaterial.SetFloat(_startTimeId, mirrorEffect.startTime.value);
                cmd.Blit(currentSource, currentDest, _mirrorMaterial);
                (currentSource, currentDest) = (currentDest, currentSource);
            }

            var scanlineEffect = stack.GetComponent<ScanlineComponent>();
            if (scanlineEffect.IsActive() && _scanlineMaterial != null)
            {
                _scanlineMaterial.SetFloat(_scanlineIntensityId, scanlineEffect.intensity.value);
                _scanlineMaterial.SetInt(_scanlineSizeId, scanlineEffect.scanlineCount.value);
                cmd.Blit(currentSource, currentDest, _scanlineMaterial);
                (currentSource, currentDest) = (currentDest, currentSource);
            }

            // Now blit the combined effects to the output texture (while clearing alpha)
            cmd.Blit(currentSource, _venueTexture, _alphaClearMaterial);

            Graphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            RenderTexture.ReleaseTemporary(rt1);
            RenderTexture.ReleaseTemporary(rt2);
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
    }
}
