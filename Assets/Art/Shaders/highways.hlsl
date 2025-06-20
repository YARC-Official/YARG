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
        
    float delta_x = abs(index * 100 - positionWS.x);
    positionWS.y += pow(delta_x, 2) * -_YargCurveFactors[index] * 0.05;

    // Present as if its a single highway, using corresponding
    // camera's matrices
    float4 clipPOS = mul(mul(_YargCamProjMatrices[index], _YargCamViewMatrices[index]), float4(positionWS, 1.0));

    return clipPOS;
}

// Tranforms position from object to homogenous space
inline float4 YargObjectToClipPos( in float3 pos )
{
    return YargTransformWorldToHClip(mul(unity_ObjectToWorld, float4(pos, 1.0)).xyz);
}
