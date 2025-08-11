#define MAX_MATRICES 128
uniform int _YargHighwaysN;
uniform float4x4 _YargCamViewMatrices[MAX_MATRICES];
uniform float4x4 _YargCamInvViewMatrices[MAX_MATRICES];
uniform float4x4 _YargCamProjMatrices[MAX_MATRICES];
uniform float _YargCurveFactors[MAX_MATRICES];
uniform float _YargFadeParams[MAX_MATRICES * 2];

// World position to highway index
inline int WorldPosToIndex(float3 positionWS)
{
    float index = (positionWS.x + 10) / 100;
    index = clamp(index, 0, _YargHighwaysN - 1);
    return index;
}

// UV to highway index
inline int UVToIndex(float2 uv)
{
    float laneWidth = 1.0 / _YargHighwaysN;
    float index = uv.x / laneWidth;
    index = clamp(index, 0, _YargHighwaysN - 1);
    return index;
}

// Default transform
inline float4 DefTransformWorldToHClip(float3 positionWS)
{
    return mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
}

inline float3 YargWorldSpaceCameraPos(float3 positionWS) {
    if (_YargHighwaysN < 1)
        return _WorldSpaceCameraPos;
    else {
        int index = WorldPosToIndex(positionWS);
        // Camera world position is the translation column of inverse view matrix
        return _YargCamInvViewMatrices[index]._m03_m13_m23;
    }
}

inline float4x4 YargCamProjMatrix(float3 positionWS)
{
    if (_YargHighwaysN > 0)
    {
        int index = WorldPosToIndex(positionWS);
        return _YargCamProjMatrices[index];
    } else {
        return UNITY_MATRIX_P;
    }
}

inline float4x4 YargViewMatrix(float3 positionWS)
{
    if (_YargHighwaysN > 0)
    {
        int index = WorldPosToIndex(positionWS);
        return _YargCamViewMatrices[index];
    } else {
        return UNITY_MATRIX_V;
    }
}

inline float3 YargTransformWorldToView(float3 positionWS)
{
    return mul(YargViewMatrix(positionWS), float4(positionWS, 1.0)).xyz;
}


#ifdef UNITY_CG_INCLUDED
// Computes world space view direction, from object space position
inline float3 YargWorldSpaceViewDir(float4 localPos)
{
    if (_YargHighwaysN > 0)
    {
        float3 worldPos = mul(unity_ObjectToWorld, localPos).xyz;
        return YargWorldSpaceCameraPos(worldPos).xyz - worldPos;
            
    } else {
        return WorldSpaceViewDir(localPos);
    }
}
#endif

#ifdef UNITY_SHADER_VARIABLES_FUNCTIONS_INCLUDED
// Computes the world space view direction (pointing towards the viewer).
inline float3 YargGetWorldSpaceViewDir(float3 positionWS)
{
    if (_YargHighwaysN > 0)
    {
        return YargWorldSpaceCameraPos(positionWS).xyz - positionWS;
    } else {
        return GetWorldSpaceViewDir(positionWS);
    }
}
#endif

// Tranforms position from world to homogenous space
inline float4 YargTransformWorldToHClip(float3 positionWS)
{
    if (_YargHighwaysN < 1)
        return DefTransformWorldToHClip(positionWS);

    int index = WorldPosToIndex(positionWS);
        
#if 1
    // d = sqrt((x - t_x) * (x - t_x) + (z - t_z) * (z - t_z))
    // (x', y', z') = sin(d / R) * (R + y - t_y) / d * (x - t_x, 0, z - t_z) + (t_x, cos(d / R) * (R + y - t_y) - R + t_y, t_z)
    // Where (x', y', z') is the transformation of (x, y, z) when curving the world around a sphere with radius R whose top is at (t_x, t_y, t_z).
    //
    // Adjusting for the circle (removing Z) we get
    // d = sqrt((x - t_x) * (x - t_x))
    // (x', y') = sin(d / R) * (R + y - t_y) / d * (x - t_x, 0) + (t_x, cos(d / R) * (R + y - t_y) - R + t_y)


    // We're not doing the sphere so from the above formula removing z components
    // TOP of the circle
    float t_x = index * 100;
    float t_y = 100;

    float R = _YargCurveFactors[index];
#define MAX_R 15.0
#define MIN_R 2.0
    
    if (R != 0)
    {
        R = sign(R) * MAX_R - R * ((MAX_R - MIN_R) / 3.0);
        // We do not want sphere, we're omiting Z component of distance
        float d = abs(positionWS.x - t_x);
        positionWS.xy = sin(d / R) * (R + positionWS.y - t_y) / d * float2(positionWS.x - t_x, 0) + float2(t_x, cos(d / R) * (R + positionWS.y - t_y) - R + t_y);
    }

#else
    // Old basic y-only parabolic shift
    float delta_x = abs(index * 100 - positionWS.x);
    positionWS.y += pow(delta_x, 2) * -_YargCurveFactors[index] * 0.05;
#endif

    // Present as if its a single highway, using corresponding
    // camera's matrices
    float4 clipPOS = mul(mul(_YargCamProjMatrices[index], _YargCamViewMatrices[index]), float4(positionWS, 1.0));
    // separate highways to avoid clashes when there are a lot of them on at the same time
    clipPOS.z -= index * 0.0002;

#ifdef _RAISE_Z
    clipPOS.z += 0.002;
#endif

    return clipPOS;
}

// Tranforms position from object to homogenous space
inline float4 YargObjectToClipPos( in float3 pos )
{
    return YargTransformWorldToHClip(mul(unity_ObjectToWorld, float4(pos, 1.0)).xyz);
}
