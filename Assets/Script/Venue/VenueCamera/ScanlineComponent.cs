using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VenueCamera
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("Venue/Scanline", typeof(UniversalRenderPipeline))]
    public class ScanlineComponent : VolumeComponent, IPostProcessComponent
    {
        public NoInterpClampedFloatParameter intensity     = new(0f, 0f, 1f);
        public NoInterpMaxIntParameter       scanlineCount = new(540, 1080);

        public bool IsActive() => intensity.value > 0;
        public bool IsTileCompatible() => true;
    }
}