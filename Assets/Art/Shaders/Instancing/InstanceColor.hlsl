#ifndef YARG_INSTANCE_COLOR_INCLUDED
#define YARG_INSTANCE_COLOR_INCLUDED

// Need to check both standard and procedural, no unified define for both
#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    StructuredBuffer<float4> _InstancedColor;
#endif

// When instancing is enabled, retrieves the color for the current instance.
// Otherwise, passes through the given color unmodified.
void GetInstanceColor_float(in const float4 In, out float4 Out)
{
#if defined(UNITY_INSTANCING_ENABLED) || defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
    Out = _InstancedColor[unity_InstanceID];
#else
    Out = In;
#endif
}

#endif