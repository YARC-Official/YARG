using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VolumeComponents
{
    [Serializable]
    [VolumeComponentMenu("Venue/Posterize")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class PosterizeComponent : VolumeComponent, IPostProcessComponent
    {
        public ClampedIntParameter Steps = new(0, 0, 100);

        public bool IsActive() => Steps.overrideState && Steps.value > 0;
        public bool IsTileCompatible() => true;
    }
}