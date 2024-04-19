#ifndef YARG_INSTANCE_COLOR_INCLUDED
#define YARG_INSTANCE_COLOR_INCLUDED

#if UNITY_ANY_INSTANCING_ENABLED
    // 100 is used as a placeholder size, the actual size
    // comes from script when setting the array
    float4 _InstancedColor[100];
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