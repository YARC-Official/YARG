using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;

namespace YARG.Venue.VenueCamera
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("Venue/Posterize", typeof(UniversalRenderPipeline))]
    public class PosterizeComponent : VolumeComponent, IPostProcessComponent
    {
        public ClampedIntParameter Steps = new(0, 0, 10);

        public bool IsActive() => Steps.overrideState && Steps.value > 0;
        public bool IsTileCompatible() => true;
    }
}