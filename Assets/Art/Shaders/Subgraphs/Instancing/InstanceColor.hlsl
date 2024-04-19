#ifndef YARG_INSTANCE_COLOR_INCLUDED
#define YARG_INSTANCE_COLOR_INCLUDED

#if UNITY_ANY_INSTANCING_ENABLED
    // TODO: Figure out how to use buffers for this
    // All current attempts have failed
    // StructuredBuffer<float4> _InstancedColor;

    // 1023 is the max length allowed for array properties
    float4 _InstancedColor[1023];
#endif

void GetInstanceColor_float(in float4 In, out float4 Out)
{
#if UNITY_ANY_INSTANCING_ENABLED
    Out = _InstancedColor[unity_InstanceID];
#else
    Out = In;
#endif
}

#endif