#define MAX_MATRICES 128
uniform int _YargHighwaysN;
uniform float _YargHighwaysScale;
uniform float4x4 _YargCamViewMatrices[MAX_MATRICES];
uniform float4x4 _YargCamInvViewMatrices[MAX_MATRICES];
uniform float4x4 _YargCamProjMatrices[MAX_MATRICES];

// World position to highway index
inline int WorldPosToIndex(float3 positionWS)
{
    float index = (positionWS.x + 10) / 100;
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

// Tranforms position from world to homogenous space
float4 YargTransformWorldToHClip(float3 positionWS)
{
    if (_YargHighwaysN < 1)
        return DefTransformWorldToHClip(positionWS);

    int index = WorldPosToIndex(positionWS);
        
    // Present as if its a single highway, using corresponding
    // camera's matrices
    float4 clipPOS = mul(mul(_YargCamProjMatrices[index], _YargCamViewMatrices[index]), float4(positionWS, 1.0));

    // Divide screen into N equal regions: [-1, 1] => 2.0 width
    float laneWidth = 2.0 / _YargHighwaysN;

    // Center of this highway’s lane in NDC:
    float centerX = -1.0 + laneWidth * (index + 0.5);
    float2 offsetNDC = float2(centerX, 1.0 - _YargHighwaysScale);
    // float2 offsetNDC = float2(centerX, 0.0);

    // Move horizontally to place according to index
    float2 offsetClip = offsetNDC * clipPOS.w;
    clipPOS.xy = clipPOS.xy * _YargHighwaysScale + offsetClip;


    return clipPOS;
}

// Tranforms position from object to homogenous space
inline float4 YargObjectToClipPos( in float3 pos )
{
    return YargTransformWorldToHClip(mul(unity_ObjectToWorld, float4(pos, 1.0)));
}
