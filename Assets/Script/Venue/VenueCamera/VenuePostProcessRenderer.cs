using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VenueCamera
{
    [System.Serializable]
    public class VenuePostProcessRenderer : ScriptableRendererFeature
    {
        private VenuePostProcessPass _pass;

        public override void Create()
        {
            _pass = new VenuePostProcessPass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}