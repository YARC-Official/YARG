using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VolumeComponents
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("Venue/Scanline", typeof(UniversalRenderPipeline))]
    public class ScanlineComponent : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity     = new(0f, 0f, 1f);
        public ClampedIntParameter   scanlineCount = new(270, 64, 540);

        public bool IsActive() => intensity.value > 0;
        public bool IsTileCompatible() => true;
    }
}