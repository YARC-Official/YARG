using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VenueCamera
{
    // TODO: Make this more generic so we can do more custom effects, not just scanline

    [System.Serializable]
    public class VenuePostProcessPass : ScriptableRenderPass
    {
        private RenderTargetIdentifier _source;
        private RenderTargetIdentifier _destinationA;
        private RenderTargetIdentifier _destinationB;
        private RenderTargetIdentifier _latestDest;
        private RenderTargetIdentifier _finalDest;

        private readonly int _temporaryRtIdA = Shader.PropertyToID("_TempRT");
        private readonly int _temporaryRtIdB = Shader.PropertyToID("_TempRTB");

        private readonly int _scanlineIntensity = Shader.PropertyToID("_ScanlineIntensity");
        private readonly int _scanlineSize = Shader.PropertyToID("_ScanlineSize");

        private readonly int _trailsLength = Shader.PropertyToID("_Length");
        private readonly int _trailsTexture = Shader.PropertyToID("_PrevFrame");

        private Shader   _trailsShader;
        private Material _trailsMaterial;
        private Shader   _scanlineShader;
        private Material _scanlineMaterial;
        private Shader   _mirrorShader;
        private Material _mirrorMaterial;

        public VenuePostProcessPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
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
                _trailsMaterial = CoreUtils.CreateEngineMaterial(_trailsShader);
            }

            _scanlineShader = Shader.Find("Scanlines");
            if (_scanlineShader == null)
            {
                Debug.LogError("Scanline shader not found!");
            }
            else
            {
                _scanlineMaterial = CoreUtils.CreateEngineMaterial(_scanlineShader);
            }

            _mirrorShader = Shader.Find("Mirror");
            if (_mirrorShader == null)
            {
                Debug.LogError("Mirror shader not found!");
            }
            else
            {
                _mirrorMaterial = CoreUtils.CreateEngineMaterial(_mirrorShader);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("CustomPostProcessPass");
            cmd.Clear();

            var stack = VolumeManager.instance.stack;

            void BlitTo(Material mat, int pass = 0)
            {
                var first = _latestDest;
                var last = first == _destinationA ? _destinationB : _destinationA;
                Blit(cmd, first, last, mat, pass);

                _latestDest = last;
            }

            _latestDest = _source;

            var trailsEffect = stack.GetComponent<TrailsComponent>();
            var material = _trailsMaterial;

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

            cmd.Blit(_latestDest, _source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(_temporaryRtIdA);
            cmd.ReleaseTemporaryRT(_temporaryRtIdB);
        }
    }
}