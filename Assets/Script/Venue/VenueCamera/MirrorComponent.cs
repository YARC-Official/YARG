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
        public BoolParameter   enabled   = new(false);
        public MaxIntParameter wipeIndex = new(3, 3);
        public FloatParameter  wipeTime  = new(0.5f);
        public FloatParameter  startTime = new(0f);

        public bool IsActive() => enabled.value && enabled.overrideState;
        public bool IsTileCompatible() => true;
    }

}