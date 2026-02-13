using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace YARG.Venue.VolumeComponents
{
    [Serializable]
    [VolumeComponentMenu("Venue/Mirror")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
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