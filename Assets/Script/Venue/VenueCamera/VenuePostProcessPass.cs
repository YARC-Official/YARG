using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VenueCamera
{

    // Note for the future: This whole thing is probably a gotcha if/when we upgrade to Unity 6.
    // 2021 doesn't really support post-postprocessing passes correctly, though it does in later
    // versions.
    //
    // This is one big fat workaround for that lack of support.
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

        // Huge Unity 6 gotcha. We have to hardcode the PP pass input/output textures because
        // cameraColorTarget always points to _CameraColorAttachmentA even after the PP pass
        private readonly int _attachmentB = Shader.PropertyToID("_CameraColorAttachmentB");

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

        private ProfilingSampler _ppProfiler;
        private ProfilingSampler _lowFpsSaveProfiler;
        private ProfilingSampler _lowFpsRestoreProfiler;
        private ProfilingSampler _finalWriteProfiler;

        public VenuePostProcessPass(ref RenderTexture stashTex)
        {
            renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            _stashTex = stashTex;
            _ppProfiler = new ProfilingSampler("VenuePostProcess");
            _lowFpsSaveProfiler = new ProfilingSampler("LowFPSSave");
            _lowFpsRestoreProfiler = new ProfilingSampler("LowFPSRestore");
            _finalWriteProfiler = new ProfilingSampler("VenueFinalWrite");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor descriptor = renderingData.cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            var renderer = renderingData.cameraData.renderer;
            // _source = renderer.cameraColorTarget;
            // This is required because PP pass outputs to _CameraColorAttachmentB, but does not
            // update cameraColorTarget
            _source = new RenderTargetIdentifier(_attachmentB);

            cmd.GetTemporaryRT(_temporaryRtIdA, descriptor, FilterMode.Point);
            _destinationA = new RenderTargetIdentifier(_temporaryRtIdA);
            cmd.GetTemporaryRT(_temporaryRtIdB, descriptor, FilterMode.Point);
            _destinationB = new RenderTargetIdentifier(_temporaryRtIdB);

            // _finalDest = new RenderTargetIdentifier(_attachmentB);

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
                    using (new ProfilingScope(cmd, _lowFpsRestoreProfiler))
                    {
                        context.ExecuteCommandBuffer(cmd);
                        cmd.Clear();
                        // We don't want to display this frame, so blit intermediate to _destinationA and carry on
                        cmd.Blit(_stashTex, _destinationA);
                        // Blitter.BlitTexture(cmd, rt, _destinationA);
                        _latestDest = _destinationA;
                    }
                }
            }

            using (new ProfilingScope(cmd, _ppProfiler))
            {
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

                var mirrorEffect = stack.GetComponent<MirrorComponent>();
                material = _mirrorMaterial;

                if (mirrorEffect.IsActive() && material != null)
                {
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

                ConfigureTarget(_source);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            using (new ProfilingScope(cmd, _lowFpsSaveProfiler))
            {
                if (saveFrame)
                {
                    // We want to save this frame, so blit to the intermediate texture
                    cmd.Blit(_latestDest, _stashTex);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }
            }

            using (new ProfilingScope(cmd, _finalWriteProfiler))
            {
                cmd.Blit(_latestDest, _source);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            // This seems unnecessary, but is somehow required to make the frame debugger work right
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
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