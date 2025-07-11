using System;
using System.Runtime.CompilerServices;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VenueCamera
{
    [Serializable]
    [VolumeComponentMenuForRenderPipeline("Venue/Mirror", typeof(UniversalRenderPipeline))]
    public class MirrorComponent : VolumeComponent, IPostProcessComponent
    {
        public BoolParameter enabled = new(false);

        public bool IsActive() => enabled.value && enabled.overrideState;
        public bool IsTileCompatible() => true;
    }

}