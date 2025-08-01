using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VenueRenderingPass
{
    [System.Serializable]
    public class VenuePostProcessRenderer : ScriptableRendererFeature
    {
        private VenuePostProcessPass _pass;
        private RenderTexture _stashTex;

        public override void Create()
        {
            // We need to create and allocate the texture
            var descriptor = new RenderTextureDescriptor(Screen.width, Screen.height, RenderTextureFormat.ARGBHalf);
            descriptor.mipCount = 0;
            _stashTex = new RenderTexture(descriptor);
            _stashTex.Create();
            _pass = new VenuePostProcessPass(ref _stashTex);
            _pass.ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}