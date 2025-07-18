using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VenueCamera
{

    [System.Serializable]
    public class VenuePostProcessPass : ScriptableRenderPass
    {
        private RenderTargetIdentifier _source;
        private RenderTargetIdentifier _destinationA;
        private RenderTargetIdentifier _destinationB;
        private RenderTargetIdentifier _latestDest;
        private RenderTargetIdentifier _finalDest;
        private RenderTargetIdentifier _lowFpsSrc;
        private RenderTargetIdentifier _lowFpsDest;

        private readonly int _temporaryRtIdA = Shader.PropertyToID("_TempRT");
        private readonly int _temporaryRtIdB = Shader.PropertyToID("_TempRTB");

        private readonly int _scanlineIntensity = Shader.PropertyToID("_ScanlineIntensity");
        private readonly int _scanlineSize = Shader.PropertyToID("_ScanlineSize");

        private readonly int _trailsLength = Shader.PropertyToID("_Length");

        private readonly int _posterizeSteps = Shader.PropertyToID("_Steps");

        private Shader   _trailsShader;
        private Material _trailsMaterial;
        private Shader   _scanlineShader;
        private Material _scanlineMaterial;
        private Shader   _mirrorShader;
        private Material _mirrorMaterial;
        private Shader   _posterizeShader;
        private Material _posterizeMaterial;

        private RenderTexture _lowFpsRenderTexture;
        private RenderTexture _stashTex;

        public VenuePostProcessPass(ref RenderTexture stashTex)
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            _stashTex = stashTex;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            var renderer = renderingData.cameraData.renderer;
            _source = renderer.cameraColorTarget;

            cmd.GetTemporaryRT(_temporaryRtIdA, descriptor, FilterMode.Point);
            _destinationA = new RenderTargetIdentifier(_temporaryRtIdA);
            cmd.GetTemporaryRT(_temporaryRtIdB, descriptor, FilterMode.Point);
            _destinationB = new RenderTargetIdentifier(_temporaryRtIdB);

            _trailsShader = Shader.Find("Trails");
            if (_trailsShader == null)
            {
                Debug.LogError("Trails shader not found!");
            }
            else
            {
                if (_trailsMaterial == null)
                {
                    _trailsMaterial = CoreUtils.CreateEngineMaterial(_trailsShader);
                }
            }

            _scanlineShader = Shader.Find("Scanlines");
            if (_scanlineShader == null)
            {
                Debug.LogError("Scanline shader not found!");
            }
            else
            {
                if (_scanlineMaterial == null)
                {
                    _scanlineMaterial = CoreUtils.CreateEngineMaterial(_scanlineShader);
                }
            }

            _mirrorShader = Shader.Find("Mirror");
            if (_mirrorShader == null)
            {
                Debug.LogError("Mirror shader not found!");
            }
            else
            {
                if (_mirrorMaterial == null)
                {
                    _mirrorMaterial = CoreUtils.CreateEngineMaterial(_mirrorShader);
                }
            }

            _posterizeShader = Shader.Find("Posterize");
            if (_posterizeShader == null)
            {
                Debug.LogError("Posterize shader not found!");
            }
            else
            {
                if (_posterizeMaterial == null)
                {
                    _posterizeMaterial = CoreUtils.CreateEngineMaterial(_posterizeShader);
                }
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("CustomPostProcessPass");
            bool saveFrame = false;
            cmd.Clear();

            var stack = VolumeManager.instance.stack;

            _latestDest = _source;

            var fpsEffect = stack.GetComponent<SlowFPSComponent>();

            if (fpsEffect.IsActive())
            {
                if (fpsEffect.LastFrame + fpsEffect.SkipFrames.value < Time.frameCount)
                {
                    // We want to display this frame, so just set a flag so we know to blit to
                    // the intermediate texture once we're done
                    fpsEffect.LastFrame = Time.frameCount;
                    saveFrame = true;
                }
                else
                {
                    // We don't want to display this frame, so blit intermediate to _destinationA and carry on
                    cmd.Blit(_stashTex, _destinationA);
                    // Blitter.BlitTexture(cmd, rt, _destinationA);
                    _latestDest = _destinationA;
                }
            }

            var posterizeEffect = stack.GetComponent<PosterizeComponent>();
            var material = _posterizeMaterial;

            if (posterizeEffect.IsActive() && material != null)
            {
                material.SetInteger(_posterizeSteps, posterizeEffect.Steps.value);
                BlitTo(material);
            }

            var trailsEffect = stack.GetComponent<TrailsComponent>();
            material = _trailsMaterial;

            if (trailsEffect.IsActive() && material != null)
            {
                material.SetFloat(_trailsLength, trailsEffect.Length);
                BlitTo(material);
            }

            var scanlineEffect = stack.GetComponent<ScanlineComponent>();
            material = _scanlineMaterial;

            if (scanlineEffect.IsActive() && material != null)
            {
                material.SetFloat(_scanlineIntensity, scanlineEffect.intensity.value);
                material.SetInt(_scanlineSize, scanlineEffect.scanlineCount.value);
                BlitTo(material);
            }

            var mirrorEffect = stack.GetComponent<MirrorComponent>();
            material = _mirrorMaterial;

            if (mirrorEffect.IsActive() && material != null)
            {
                BlitTo(material);
            }

            if (saveFrame)
            {
                // We want to save this frame, so blit to the intermediate texture
                cmd.Blit(_latestDest, _stashTex);
            }

            cmd.Blit(_latestDest, _source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            return;

            void BlitTo(Material mat, int pass = 0)
            {
                var first = _latestDest;
                var last = first == _destinationA ? _destinationB : _destinationA;
                Blit(cmd, first, last, mat, pass);

                _latestDest = last;
            }
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_temporaryRtIdA);
            cmd.ReleaseTemporaryRT(_temporaryRtIdB);

            if (_lowFpsRenderTexture != null)
            {
                _lowFpsRenderTexture.Release();
                _lowFpsRenderTexture = null;
            }

            // TODO: Determine if this is actually necessary to avoid leaking memory
            // Sure would be nice to be able to destroy them when the venue gets unloaded instead
            CoreUtils.Destroy(_trailsMaterial);
            CoreUtils.Destroy(_scanlineMaterial);
            CoreUtils.Destroy(_mirrorMaterial);
            CoreUtils.Destroy(_posterizeMaterial);
        }
    }
}