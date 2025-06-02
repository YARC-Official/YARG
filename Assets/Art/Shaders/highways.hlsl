#define MAX_MATRICES 128
uniform int _YargHighwaysN;
uniform float _YargHighwaysScale;
uniform float4x4 _YargCamViewMatrices[MAX_MATRICES];
uniform float4x4 _YargCamProjMatrices[MAX_MATRICES];

int WorldPosToIndex(float3 positionWS)
{
    float index = (positionWS.x + 10) / 100;
    index = clamp(index, 0, _YargHighwaysN - 1);
    return index;
}

float4 YargTransformWorldToHClip(float3 positionWS)
{
    if (_YargHighwaysN == 0)
        return TransformWorldToHClip(positionWS);

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


