using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private static int                     activeInstances = 0;

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

        private int _fps;
        private int _venueLayerMask;

        private bool _didRender;

        private void Awake()
        {
            renderScale = GraphicsManager.Instance.VenueRenderScale;
            _renderCamera = GetComponent<Camera>();
            // Disable the camera so we can control when it renders
            _renderCamera.enabled = false;

            _renderCamera.allowMSAA = false;
            RenderPipelineManager.beginCameraRendering += OnPreCameraRender;
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
            RenderPipelineManager.endCameraRendering += OnPostCameraRender;
            UniversalRenderPipelineAsset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            _originalFactor = UniversalRenderPipelineAsset.renderScale;

            _fps = SettingsManager.Settings.VenueFpsCap.Value;
            _venueLayerMask = LayerMask.GetMask("Venue");

            var venueOutputObject = GameObject.Find("Venue Output");
            if (venueOutputObject != null)
            {
                _venueOutput = venueOutputObject.GetComponent<RawImage>();

                if (_venueOutput != null)
                {
                    _venueTexture = new RenderTexture(Screen.width, Screen.height, 0);
                    _venueOutput.texture = _venueTexture;

                    _trailsTexture = new RenderTexture(_venueTexture);
                    _trailsTexture.filterMode = FilterMode.Bilinear;
                    _trailsTexture.wrapMode = TextureWrapMode.Clamp;
                    _trailsTexture.Create();

                    Graphics.Blit(Texture2D.blackTexture, _trailsTexture);
                }
            }

            CreateMaterials();
        }

        private void OnEnable()
        {
            activeInstances++;

            if (activeInstances == 1)
            {

            }

            _cts?.Cancel();
            _cts?.Dispose();

            _fps = SettingsManager.Settings.VenueFpsCap.Value;
            _cts = new CancellationTokenSource();
            RenderLoop(_cts.Token).Forget();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnPreCameraRender;
            RenderPipelineManager.endCameraRendering -= OnPostCameraRender;

            activeInstances--;

            if (activeInstances == 0)
            {
                _cts?.Cancel();
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void OnDestroy()
        {
            UniversalRenderPipelineAsset.renderScale = _originalFactor;

            if (_venueTexture != null)
            {
                _venueTexture.Release();
                _venueTexture = null;
            }

            if (_trailsTexture != null)
            {
                _trailsTexture.Release();
                _trailsTexture = null;
            }

            _venueOutput = null;

            CoreUtils.Destroy(_trailsMaterial);
            CoreUtils.Destroy(_scanlineMaterial);
            CoreUtils.Destroy(_mirrorMaterial);
            CoreUtils.Destroy(_posterizeMaterial);
            CoreUtils.Destroy(_alphaClearMaterial);
        }

        private async UniTask RenderLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TimeSpan interval;
                var stack = VolumeManager.instance.stack;

                VolumeManager.instance.Update(_renderCamera.gameObject.transform, _venueLayerMask);

                var effectiveFps = _fps;

                var fpsEffect = stack.GetComponent<SlowFPSComponent>();

                if (fpsEffect.IsActive())
                {
                    // The divisor is relative to 60 fps, so we need to adjust for that if _fps is something other than 60
                    var fpsRatio = _fps / 60f;
                    var adjustedDivisor = fpsRatio * fpsEffect.Divisor.value;
                    effectiveFps = Mathf.RoundToInt(_fps / adjustedDivisor);
                    interval = TimeSpan.FromSeconds(1f / effectiveFps);
                }
                else
                {
                    interval = TimeSpan.FromSeconds(1f / _fps);
                }

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

                try
                {
                    await UniTask.Delay(interval, cancellationToken: token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private void OnPreCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == _renderCamera)
            {
                UniversalRenderPipelineAsset.renderScale = renderScale;
            }
        }

        private void OnPostCameraRender(ScriptableRenderContext ctx, Camera cam)
        {
            if (cam == _renderCamera)
            {
                UniversalRenderPipelineAsset.renderScale = _originalFactor;
            }
        }

        private void CreateMaterials()
        {
            _trailsMaterial = CreateMaterial("Trails");
            _scanlineMaterial = CreateMaterial("Scanlines");
            _mirrorMaterial = CreateMaterial("Mirror");
            _posterizeMaterial = CreateMaterial("Posterize");
            _alphaClearMaterial = CreateMaterial("Hidden/AlphaClear");
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
